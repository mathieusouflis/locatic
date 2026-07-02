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

Fichier : `.github/workflows/ci.yml`. Déclenché sur `pull_request` et sur `push` vers `main`. Organisation cible, avec dépendances explicites (`needs:`) :

```
test ──► docker-build ──► scan ──► publish   (publish : uniquement sur main)
```

### 1. `test` — build & tests
Déjà en place : checkout, setup .NET, `dotnet restore`, `dotnet build --configuration Release`, `dotnet test`, vérification des migrations EF. Si une étape échoue, le pipeline s'arrête : **un pipeline vert doit prouver que l'application fonctionne**, pas seulement qu'elle compile.

### 2. `docker-build` — build de l'image

```yaml
docker-build:
  needs: test
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - uses: docker/setup-buildx-action@v3
    - uses: docker/build-push-action@v6
      with:
        context: app
        push: false
        tags: ghcr.io/${{ github.repository }}:${{ github.sha }}
        outputs: type=docker,dest=/tmp/image.tar
    - uses: actions/upload-artifact@v4
      with: { name: docker-image, path: /tmp/image.tar }
```

L'image est construite **sur toutes les PR** (on valide le Dockerfile avant merge) mais n'est **publiée que sur `main`**.

### 3. `scan` — sécurité

```yaml
scan:
  needs: docker-build
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v7
    - uses: actions/download-artifact@v4
      with: { name: docker-image, path: /tmp }
    - run: docker load -i /tmp/image.tar
    - name: Scan image (Trivy)
      uses: aquasecurity/trivy-action@master
      with:
        image-ref: ghcr.io/${{ github.repository }}:${{ github.sha }}
        severity: CRITICAL,HIGH
        exit-code: "1"          # échec du pipeline si vulnérabilité critique
    - name: Scan secrets (Gitleaks)
      uses: gitleaks/gitleaks-action@v2
```

Deux scans : vulnérabilités de l'image (Trivy) et secrets dans le dépôt (Gitleaks). `exit-code: "1"` rend le scan bloquant.

### 4. `publish` — publication sur GHCR, uniquement sur `main`

```yaml
publish:
  needs: scan
  if: github.ref == 'refs/heads/main'
  runs-on: ubuntu-latest
  permissions:
    contents: read
    packages: write
  steps:
    - uses: actions/checkout@v7
    - uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}
    - uses: docker/build-push-action@v6
      with:
        context: app
        push: true
        tags: |
          ghcr.io/${{ github.repository }}:${{ github.sha }}
          ghcr.io/${{ github.repository }}:latest
```

Points clés :
- `if: github.ref == 'refs/heads/main'` — les PR construisent mais ne publient pas ;
- `permissions: packages: write` — le `GITHUB_TOKEN` éphémère du workflow suffit pour GHCR : **aucun token personnel à créer ni à stocker** ;
- double tag `sha` + `latest` : le déploiement épingle un SHA précis (traçabilité, rollback), `latest` sert de commodité locale.

Vérifier la publication : onglet **Packages** du dépôt, ou `docker pull ghcr.io/<owner>/locatic:<sha>`.

## Limites du pipeline GitHub

Le pipeline **s'arrête après la publication**. Il ne lance ni `terraform apply`, ni `ansible-playbook`, ni `kubectl` : minikube tourne sur le poste local, injoignable depuis les runners GitHub. La suite du chemin est documentée dans [deploiement-local.md](deploiement-local.md).

## Gestion des secrets

| Donnée | Où elle vit | Jamais dans Git |
|--------|-------------|-----------------|
| Auth registry (GHCR) | `GITHUB_TOKEN` automatique du workflow | token personnel |
| Variables locales sensibles | `*.tfvars`, `.env` (gitignorés) | `.env` réel |
| État Terraform | local, gitignoré (`*.tfstate*`) | `terraform.tfstate` |
| Secrets Kubernetes | templates versionnés, valeurs injectées au déploiement | Secret « réel » en clair |

Le `.gitignore` du dépôt couvre déjà tous ces motifs. En cas de secret commité par erreur : le retirer **ne suffit pas** (il reste dans l'historique) — révoquer le secret immédiatement, puis purger l'historique (`git filter-repo`).
