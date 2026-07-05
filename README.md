# Locatic (Mini-projet DevOps)

[![CI](https://github.com/mathieusouflis/locatic/actions/workflows/ci.yml/badge.svg)](https://github.com/mathieusouflis/locatic/actions/workflows/ci.yml)

Chaîne DevOps autour de **Locatic**, une application ASP.NET Core MVC de location de voitures (projet de POO) : gestion du parc, des clients et des réservations, persistance SQLite via Entity Framework Core.

Le projet couvre : Git par Pull Requests, pipeline CI/CD GitHub Actions (tests, build, scan, publication d'image Docker), déploiement local sur **minikube** via **Terraform** puis **Ansible**, reverse proxy **Nginx**, monitoring **Prometheus / Grafana**.

## Lien avec le projet de POO

Le code applicatif (`app/locatic/`) est repris du projet de POO et adapté au déploiement :

- chemin de la base SQLite configurable par variable d'environnement (`DB_PATH`), pour monter un volume ;
- endpoint `/health` (vérifie aussi l'accès à la base) pour les probes Kubernetes ;
- redirection HTTPS désactivée en conteneur (le TLS est géré par Nginx) ;
- tests dans `app/Locatic.Tests/`.

## Prérequis locaux

.NET SDK 8.0, Docker, minikube + kubectl, Terraform, Ansible, Helm (bonus).

## Structure du dépôt

```
locatic/
├── app/
│   ├── locatic/            # Application ASP.NET Core MVC (projet de POO)
│   └── Locatic.Tests/      # Tests unitaires + intégration (xUnit)
├── .github/workflows/      # Pipelines GitHub Actions (CI, release)
├── infra/
│   ├── terraform/          # Namespace, stockage persistant SQLite, outputs
│   ├── ansible/            # Playbook d'orchestration du déploiement local
│   ├── kubernetes/         # Manifests : app, Nginx, volumes, monitoring
│   └── helm/               # Chart Helm (bonus)
├── docs/                   # Documentation détaillée
└── locatic.sln
```

## Démarrage rapide (application seule)

```bash
git clone <url-du-repo>
cd locatic

dotnet run --project app/locatic   # premier lancement : migrations + données de départ
```

Application sur `http://localhost:5118`, santé sur `http://localhost:5118/health`.

```bash
dotnet test
dotnet build
```

## Déploiement complet

PR, CI verte, merge sur `main`, image publiée, puis depuis votre machine :

```bash
cd infra/terraform && terraform init && terraform apply
cd ../ansible && ansible-playbook site.yml -i inventory.yml -e app_tag=manual
```

Ordre exact, vérifications et dépannage : [docs/deploiement-local.md](docs/deploiement-local.md).

## Documentation

- [docs/architecture.md](docs/architecture.md) : architecture cible, rôle de chaque brique
- [docs/ci-cd.md](docs/ci-cd.md) : protection de `main`, PR, jobs du pipeline, secrets, registry
- [docs/docker.md](docs/docker.md) : Dockerfile expliqué, vérification locale du conteneur
- [docs/deploiement-local.md](docs/deploiement-local.md) : de l'image publiée à l'app déployée sur minikube
- [docs/terraform.md](docs/terraform.md) : ressources gérées, variables, outputs, état
- [docs/ansible.md](docs/ansible.md) : rôle du playbook, dépendance aux outputs Terraform
- [docs/kubernetes.md](docs/kubernetes.md) : ressources K8s, reverse proxy Nginx, stockage SQLite
- [docs/monitoring.md](docs/monitoring.md) : Prometheus, Grafana, métriques, alertes
- [docs/helm.md](docs/helm.md) : chart Helm (bonus)
- [docs/exploitation.md](docs/exploitation.md) : vérifications post-déploiement, logs, rollback, diagnostic
- [docs/preuves/](docs/preuves/) : captures et preuves des étapes importantes

## Gestion des secrets

Aucun secret versionné : tokens registry dans les secrets GitHub Actions, état Terraform et `*.tfvars` ignorés par Git, Secrets Kubernetes fournis sous forme de templates. Détail dans [docs/ci-cd.md](docs/ci-cd.md#gestion-des-secrets).

## Limites connues

- Le pipeline GitHub s'arrête après la publication de l'image : minikube tourne sur le poste local, les runners GitHub ne peuvent pas y accéder. Le déploiement se déclenche localement (Terraform puis Ansible).
- SQLite en volume persistant impose un seul replica applicatif en écriture (pas de HA).
- Le monitoring est local au cluster minikube (pas de rétention longue durée).

## License

Distribué sous [MIT License](LICENSE).
