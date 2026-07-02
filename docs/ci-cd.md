# CI/CD — GitHub Actions, Pull Requests et registry

## Règles de branche

La branche `main` est protégée. Configuration attendue (Settings → Branches → *Add branch ruleset* ou *Branch protection rule* sur `main`) :

1. **Require a pull request before merging** — interdit les push directs ; tout changement passe par une PR.
2. **Require status checks to pass before merging** — sélectionner les checks du workflow CI (au minimum le job de build/test). Cocher *Require branches to be up to date* pour éviter de merger du code testé sur une base obsolète.
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

## Jobs du pipeline

Trois fichiers composent le pipeline :

- `.github/workflows/ci.yml` — l'orchestrateur, déclenché sur `pull_request` et sur `push` vers `main` ;
- `.github/workflows/reusable-docker.yml` — un **workflow réutilisable** (`workflow_call`) qui construit (et publie sur demande) l'image Docker ;
- `.github/actions/setup-tools/action.yml` — une **action composite** qui installe et vérifie les outils utilisés par le job de tests (`dotnet-ef`, Docker).

Organisation, avec dépendances explicites (`needs:`) :

```
test ──► build (reusable-docker.yml, push conditionné à main) ──► security
```

### 1. `test` — build & tests

```yaml
test:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - uses: actions/setup-dotnet@v5
      with: { dotnet-version: "8.0.x" }
    - uses: ./.github/actions/setup-tools
    - run: dotnet restore
    - run: dotnet build --no-restore --configuration Release
    - name: Run tests
      run: |
        set -o pipefail
        dotnet test --no-restore --configuration Release \
          --logger "trx;LogFileName=test-results.trx" 2>&1 | tee test-output.txt
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: test-results
        path: |
          test-output.txt
          **/TestResults/*.trx
    - run: dotnet ef migrations list --project app/locatic
```

`./.github/actions/setup-tools` installe `dotnet-ef` et affiche les versions des outils utilisés (lisibilité des logs). Les résultats de test sont uploadés **même en cas d'échec** (`if: always()`) pour pouvoir diagnostiquer un test cassé sans relancer le job. Si une étape échoue, le pipeline s'arrête : **un pipeline vert doit prouver que l'application fonctionne**, pas seulement qu'elle compile.

### 2. `build` — image Docker via un workflow réutilisable

```yaml
build:
  needs: test
  uses: ./.github/workflows/reusable-docker.yml
  with:
    image-name: ghcr.io/${{ github.repository }}
    context: ./app
    push: ${{ github.ref == 'refs/heads/main' }}
  secrets:
    registry-username: ${{ github.actor }}
    registry-password: ${{ secrets.GITHUB_TOKEN }}
```

`reusable-docker.yml` factorise le build Docker (utilisable par d'autres workflows si besoin) : `docker/metadata-action` calcule les tags (`sha`, nom de branche, `latest` seulement si `push`), `docker/setup-buildx-action` + cache GitHub Actions (`cache-from`/`cache-to: type=gha`) accélèrent les builds successifs, et l'image n'est **poussée sur la registry que si `push: true`** — c'est-à-dire uniquement sur `main`. Sur une PR, l'image est construite (le Dockerfile est validé) mais jamais publiée.

Authentification : `registry-password: ${{ secrets.GITHUB_TOKEN }}` — le token éphémère du workflow, combiné à `permissions: packages: write` déclaré dans `ci.yml`, suffit pour GHCR : **aucun token personnel à créer ni à stocker**.

Vérifier la publication : onglet **Packages** du dépôt, ou `docker pull ghcr.io/<owner>/<repo>:<sha>`.

### 3. `security` — scans

```yaml
security:
  needs: build
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - name: Scan application files (Trivy)
      uses: aquasecurity/trivy-action@v0.36.0
      with:
        scan-type: 'fs'
        scan-ref: 'app/'
        severity: 'HIGH,CRITICAL'
        exit-code: '1'          # échec du pipeline si vulnérabilité critique
        format: 'table'
    - name: Scan for leaked secrets (Gitleaks)
      uses: gitleaks/gitleaks-action@v2
```

Deux scans : dépendances/fichiers vulnérables (Trivy, mode `fs` sur `app/`, couvre les packages NuGet) et secrets présents dans le dépôt (Gitleaks). `exit-code: "1"` rend le scan de vulnérabilités bloquant.

## Limites du pipeline GitHub

Le pipeline **s'arrête après le scan de sécurité** (qui suit la publication conditionnelle de l'image). Il ne lance ni `terraform apply`, ni `ansible-playbook`, ni `kubectl` : minikube tourne sur le poste local, injoignable depuis les runners GitHub. La suite du chemin est documentée dans [deploiement-local.md](deploiement-local.md).

> Volontairement, ce pipeline n'ajoute pas de jobs `deploy-staging` / `deploy-production` (même sous forme de simples `echo` de démonstration) : le sujet impose explicitement que le pipeline GitHub s'arrête après contrôles, build, scan et publication. Le déploiement réel est déclenché **depuis le poste local**, jamais depuis GitHub Actions.

## Gestion des secrets

| Donnée | Où elle vit | Jamais dans Git |
|--------|-------------|-----------------|
| Auth registry (GHCR) | `GITHUB_TOKEN` automatique du workflow | token personnel |
| Variables locales sensibles | `*.tfvars`, `.env` (gitignorés) | `.env` réel |
| État Terraform | local, gitignoré (`*.tfstate*`) | `terraform.tfstate` |
| Secrets Kubernetes | templates versionnés, valeurs injectées au déploiement | Secret « réel » en clair |

Le `.gitignore` du dépôt couvre déjà tous ces motifs. En cas de secret commité par erreur : le retirer **ne suffit pas** (il reste dans l'historique) — révoquer le secret immédiatement, puis purger l'historique (`git filter-repo`).
