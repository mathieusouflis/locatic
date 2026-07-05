# CI/CD (GitHub Actions, Pull Requests, registry)

## Protéger la branche main

Le sujet impose qu'on ne puisse pas pousser directement sur `main` : tout passe par une Pull Request. Configuré dans Settings > Branches :

1. **Require a pull request before merging** : bloque les push directs.
2. **Require status checks to pass before merging** : checks `test` et `build-app` requis, avec *Require branches to be up to date* coché.
3. **Require linear history** : merges en squash ou rebase.
4. Pas de bypass pour les admins, sinon la règle ne sert à rien.

Capture dans `docs/preuves/`.

## Le workflow suivi pour chaque changement

```bash
git checkout -b feat/ma-modification
git add . && git commit -m "feat: ma modification"
git push origin feat/ma-modification
gh pr create --fill
```

La CI se déclenche automatiquement dès l'ouverture de la PR. Merge possible seulement si tous les checks passent.

## D'où vient la structure du pipeline

Je me suis inspiré du pipeline vu en cours (jobs, workflow réutilisable, action composite), adapté sur trois points : `commitlint` ajouté en première étape (un workflow séparé existait déjà dans le dépôt, je l'ai intégré à la chaîne) ; publication réelle de l'image sur `main` (demandée par le sujet) ; `test` et `build-app` séparés en deux jobs pour distinguer clairement échec de tests et échec de compilation.

## Les fichiers du pipeline

- `.github/workflows/ci.yml` : orchestre tous les jobs
- `.github/workflows/reusable-docker.yml` : workflow réutilisable (`workflow_call`) qui construit l'image Docker
- `.github/actions/setup-tools/action.yml` : action composite qui installe/vérifie Terraform, Docker, kubectl (pas encore appelée par un job, gardée prête)

Enchaînement des jobs (`needs:`) :

commitlint, puis test, puis build-app, puis build-image (reusable-docker.yml), puis security, puis deploy-staging, puis deploy-production.

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

Ne tourne que sur les PR (il vérifie une plage de commits). Sur un push direct vers `main`, il est *skipped*, pas *failed* : le job `test` accepte donc `success` ou `skipped` comme feu vert.

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

`actions/upload-artifact` a `if: always()` : les logs sont uploadés même si les tests échouent, pratique pour ne pas relancer tout le job. `dotnet test` compile le projet en interne, mais ce n'est pas la même chose que le job `build-app` suivant : deux causes d'échec différentes, deux checks séparés.

### 3. `build-app`

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

En plus du build, ce job vérifie que les migrations Entity Framework sont à jour. Si un modèle a changé sans migration générée, `dotnet ef migrations list` échoue et le signale dans la CI.

### 4. `build-image`

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

Appelle `reusable-docker.yml`, qui utilise `docker/metadata-action` pour les tags et `docker/setup-buildx-action` avec le cache GitHub Actions (`type=gha`). `push` n'est activé que sur `main` : sur une PR l'image est construite (ce qui valide le Dockerfile, y compris son étage `test`, voir [docker.md](docker.md)) mais jamais publiée.

Authentification via `secrets.GITHUB_TOKEN` (auto-généré, combiné à `permissions: packages: write` en haut de `ci.yml`) : aucun token personnel à créer.

Pour vérifier qu'une image est publiée : onglet **Packages** du dépôt, ou `docker pull ghcr.io/<owner>/<repo>:<sha>`.

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

Trivy scanne le système de fichiers `app/` (pas l'image construite), ce qui couvre les packages NuGet vulnérables. `exit-code: "1"` rend le job bloquant.

### 6-7. `deploy-staging` / `deploy-production`

```yaml
deploy-staging:
  needs: [build-image, security]
  runs-on: ubuntu-latest
  environment: staging
  steps:
    - name: Show deployment configuration
      env:
        DB_PATH: /data/locatic.db
        ASPNETCORE_ENVIRONMENT: Production
      run: |
        echo "Image: ${{ needs.build-image.outputs.image-tag }}"
        echo "DB_PATH=$DB_PATH"
        echo "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT"
    - run: echo "Deploying to staging... (placeholder, pas de déploiement réel depuis GitHub)"

deploy-production:
  needs: deploy-staging
  runs-on: ubuntu-latest
  environment: production
  steps:
    - run: echo "Deploying to production... (placeholder, pas de déploiement réel depuis GitHub)"
```

Ces jobs ne font rien de réel, volontairement : le sujet impose que le déploiement (minikube) soit déclenché depuis mon poste, via Terraform puis Ansible, jamais depuis GitHub Actions (voir [deploiement-local.md](deploiement-local.md)). Ils servent à illustrer les **GitHub Environments**. Au départ ils affichaient un `DB_PASSWORD`/`API_KEY` de démo repris de l'exemple du cours ; ça ne correspondait à rien de réel pour Locatic (pas de base externe, pas d'API tierce), donc je les ai remplacés par le tag d'image et les vraies variables d'environnement de l'app (`DB_PATH`, `ASPNETCORE_ENVIRONMENT`, voir [.env.example](../.env.example)).

## Gestion des secrets

| Donnée | Où elle vit |
|--------|-------------|
| Auth registry (GHCR) | `GITHUB_TOKEN` automatique du workflow |
| Variables locales sensibles | `*.tfvars`, `.env` (gitignorés) |
| État Terraform | local, gitignoré (`*.tfstate*`) |
| Secrets Kubernetes | templates versionnés, valeurs injectées au déploiement |

Locatic n'a aucun vrai secret de déploiement à gérer (pas de mot de passe base de données, pas de clé d'API externe) : le seul secret manipulé par la CI est le `GITHUB_TOKEN` auto pour publier l'image.

Si un secret finissait par être commité par erreur : le retirer dans un commit suivant ne suffit pas, il reste dans l'historique. Il faut le révoquer puis purger l'historique (`git filter-repo`).
