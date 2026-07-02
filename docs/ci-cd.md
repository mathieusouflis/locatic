# CI/CD — GitHub Actions, Pull Requests et registry

## Règles de branche

La branche `main` est protégée. Configuration attendue (Settings → Branches → *Add branch ruleset* ou *Branch protection rule* sur `main`) :

1. **Require a pull request before merging** — interdit les push directs ; tout changement passe par une PR.
2. **Require status checks to pass before merging** — sélectionner les checks du workflow CI (au minimum `test`, `build-app`). Cocher *Require branches to be up to date* pour éviter de merger du code testé sur une base obsolète.
3. **Require linear history** (recommandé) — merges en squash ou rebase, historique lisible.
4. Ne pas cocher « Allow bypass » pour les admins si l'on veut prouver que la règle s'applique à tout le monde.

> À faire une seule fois dans l'interface GitHub ; joindre une capture dans `docs/preuves/`.

## Workflow de travail

```bash
git checkout -b feat/ma-modification
# ... modifications ...
git add . && git commit -m "feat: ma modification"
git push origin feat/ma-modification
gh pr create --fill
```

La PR déclenche la CI ; le merge n'est possible que si les checks passent. La première PR du projet sert de démonstration du fonctionnement (capture dans `docs/preuves/`).

## Origine du pipeline

Ce pipeline reprend, presque à l'identique, la structure du projet vu en cours (`CamillePaillou/devops-training`) : mêmes stages, même découpage en workflow réutilisable + action composite. Trois différences assumées et documentées ci-dessous : `commitlint` en première étape (convention déjà présente dans ce dépôt avant ce travail CI, absente de la référence), la publication réelle de l'image (la référence ne pousse jamais son image), et le job `test` scindé en `test` + `build-app` pour donner un check GitHub distinct à « les tests passent » et à « le projet compile » (la référence n'a pas cette distinction : `npm test` ne nécessite pas de compilation séparée).

## Fichiers du pipeline

- `.github/workflows/ci.yml` — l'orchestrateur ;
- `.github/workflows/reusable-docker.yml` — workflow réutilisable (`workflow_call`) qui construit l'image ;
- `.github/actions/setup-tools/action.yml` — action composite installant/vérifiant des outils DevOps (Terraform, Docker, kubectl). Comme dans la référence, elle n'est pour l'instant **appelée par aucun job** de `ci.yml` : elle est prête à être réutilisée par un futur workflow (ou en local) sans polluer le job de tests.

Organisation, avec dépendances explicites (`needs:`) :

```
commitlint ──► test ──► build-app ──► build-image (reusable-docker.yml) ──► security ──► deploy-staging ──► deploy-production
```

### 1. `commitlint`

```yaml
commitlint:
  if: github.event_name == 'pull_request'
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
      with: { fetch-depth: 0 }
    - uses: wagoid/commitlint-github-action@v6
      with:
        failOnWarnings: false
        helpURL: https://www.conventionalcommits.org
```

Reprend un workflow qui existait déjà séparément dans ce dépôt (`.github/workflows/commitlint.yml`, absent de la référence) et l'intègre comme première étape d'une seule chaîne au lieu d'un check indépendant. Il ne s'applique qu'aux PR (une plage de commits à valider). Sur un push direct vers `main`, le job est *skipped* — le `if:` de `test` traite `success` et `skipped` comme un feu vert, pour ne pas bloquer les pushes sur `main`.

### 2. `test`

```yaml
test:
  needs: commitlint
  if: |
    always() &&
    (needs.commitlint.result == 'success' || needs.commitlint.result == 'skipped')
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - uses: actions/setup-dotnet@v5
      with: { dotnet-version: "8.0.x" }
    - run: dotnet restore
    - name: Run tests
      run: |
        set -o pipefail
        dotnet test --configuration Release \
          --logger "trx;LogFileName=test-results.trx" 2>&1 | tee test-output.txt
    - uses: actions/upload-artifact@v4
      if: always()
      with: { name: test-output, path: test-output.txt }
```

La référence fait tourner ses tests Node sur une **matrice de versions** (`node-version: [18, 20, 22]`) parce qu'une lib npm doit rester compatible avec plusieurs runtimes Node en usage réel. Ce n'est pas le cas ici : le projet cible une seule version du SDK .NET (celle du `TargetFramework` du `.csproj`), donc pas de matrice — un seul job suffit. Le reste du pattern est identique : sortie de test capturée via `tee` puis uploadée comme artefact **même en cas d'échec** (`if: always()`), pour diagnostiquer un test cassé sans relancer le job.

`dotnet test` compile ce dont il a besoin en interne (impossible d'exécuter des tests sans compiler) ; ce n'est volontairement pas la même chose que le check « le projet applicatif compile » ci-dessous, qui a son propre nom de check dans l'UI GitHub.

### 3. `build-app` — compilation du projet .NET

```yaml
build-app:
  needs: test
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - uses: actions/setup-dotnet@v5
      with: { dotnet-version: "8.0.x" }
    - run: dotnet restore
    - run: dotnet build --no-restore --configuration Release
    - run: |
        dotnet tool install --global dotnet-ef
        dotnet ef migrations list --project app/locatic
```

Étape sans équivalent dans la référence (JavaScript n'a pas de phase de compilation à valider séparément). Elle donne un signal distinct de `test` dans l'UI GitHub : un build cassé et un test cassé sont deux causes d'échec différentes, autant les voir comme deux checks différents. La vérification des migrations EF (`dotnet ef migrations list`) vit ici : elle échoue si le modèle a changé sans migration générée, ce qui est un problème de « le projet est dans un état cohérent », pas un test à proprement parler.

### 4. `build-image` — image Docker via un workflow réutilisable

```yaml
build-image:
  needs: build-app
  uses: ./.github/workflows/reusable-docker.yml
  with:
    image-name: ghcr.io/${{ github.repository }}
    context: ./app
    push: ${{ github.ref == 'refs/heads/main' }}
  secrets:
    registry-username: ${{ github.actor }}
    registry-password: ${{ secrets.GITHUB_TOKEN }}
```

`reusable-docker.yml` reprend le pattern de la référence (`docker/metadata-action` pour les tags, `docker/setup-buildx-action` + cache GitHub Actions `type=gha`) avec **une différence assumée** : la référence construit toujours avec `push: false` (elle ne publie jamais son image, y compris sur `main`). Le sujet de ce mini-projet exige au contraire une publication réelle sur la registry lors du merge sur `main` — le workflow réutilisable a donc été étendu avec une entrée `push` (booléenne, `false` par défaut) que `ci.yml` active uniquement quand `github.ref == 'refs/heads/main'`. Sur une PR, l'image est construite (le Dockerfile est validé, y compris son étage `test` — voir [docker.md](docker.md)) mais jamais publiée.

Authentification : `registry-password: ${{ secrets.GITHUB_TOKEN }}` — le token éphémère du workflow, combiné à `permissions: packages: write` déclaré dans `ci.yml`, suffit pour GHCR : **aucun token personnel à créer ni à stocker**.

Vérifier la publication : onglet **Packages** du dépôt, ou `docker pull ghcr.io/<owner>/<repo>:<sha>`.

### 5. `security`

```yaml
security:
  needs: build-image
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - name: Scan application files
      uses: aquasecurity/trivy-action@v0.36.0
      with:
        scan-type: 'fs'
        scan-ref: 'app/'
        severity: 'HIGH,CRITICAL'
        exit-code: '1'
        format: 'table'
```

Identique à la référence : scan **filesystem** (pas de l'image construite) sur `app/`, couvre les packages NuGet vulnérables via le fichier de restauration. `exit-code: "1"` rend le job bloquant.

### 6-7. `deploy-staging` / `deploy-production`

```yaml
deploy-staging:
  needs: [build-image, security]
  runs-on: ubuntu-latest
  environment: staging
  steps:
    - name: Use secrets
      env:
        DB_PASSWORD: ${{ secrets.DB_PASSWORD }}
        API_KEY: ${{ secrets.API_KEY }}
      run: |
        echo "Secrets are masked in logs"
        echo "DB_PASSWORD length: ${#DB_PASSWORD}"
    - run: echo "Deploying to staging... (placeholder, pas de déploiement réel depuis GitHub)"

deploy-production:
  needs: deploy-staging
  runs-on: ubuntu-latest
  environment: production
  steps:
    - run: echo "Deploying to production... (placeholder, pas de déploiement réel depuis GitHub)"
```

Repris tels quels de la référence : deux jobs de démonstration qui illustrent l'usage des **GitHub Environments** et des secrets scopés par environnement (masqués dans les logs), sans exécuter aucune action réelle. **Ils ne déploient rien** : ni `kubectl`, ni `terraform`, ni `ansible` ne sont invoqués. Le sujet de ce mini-projet impose que le déploiement effectif (sur minikube) soit déclenché **depuis le poste local**, via Terraform puis Ansible — voir [deploiement-local.md](deploiement-local.md). Ces deux jobs restent donc volontairement inertes ; ne pas les confondre avec un vrai pipeline de déploiement continu.

## Gestion des secrets

| Donnée | Où elle vit | Jamais dans Git |
|--------|-------------|-----------------|
| Auth registry (GHCR) | `GITHUB_TOKEN` automatique du workflow | token personnel |
| `DB_PASSWORD` / `API_KEY` (démonstration `deploy-staging`) | secrets GitHub scopés à l'environnement `staging` | valeur en clair dans le YAML |
| Variables locales sensibles | `*.tfvars`, `.env` (gitignorés) | `.env` réel |
| État Terraform | local, gitignoré (`*.tfstate*`) | `terraform.tfstate` |
| Secrets Kubernetes | templates versionnés, valeurs injectées au déploiement | Secret « réel » en clair |

Le `.gitignore` du dépôt couvre déjà tous ces motifs. En cas de secret commité par erreur : le retirer **ne suffit pas** (il reste dans l'historique) — révoquer le secret immédiatement, puis purger l'historique (`git filter-repo`).
