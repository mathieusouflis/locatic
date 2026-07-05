# Helm (bonus)

## Pourquoi ce bonus

+2 points, et un vrai intérêt pratique : au lieu d'appliquer les templates Jinja2 rôle par rôle avec Ansible, une seule commande (`helm upgrade --install`) installe ou met à jour tout le déploiement, avec historique des releases et `helm rollback` (couvre au passage le bonus rollback).

Je suis parti du chart `devops-app-chart` vu en cours (même structure avec `_helpers.tpl`, mêmes conventions de labels, même pattern `envFrom`/ConfigMap), avec deux différences : pas de `secret.yaml` ni `postgres.yaml` (pas de base externe à provisionner, voir [architecture.md](architecture.md)) ; templates Nginx ajoutés, absents de l'exemple du cours qui expose son app directement.

## Structure du chart (`infra/helm/locatic/`)

```
infra/helm/locatic/
├── Chart.yaml
├── values.yaml
└── templates/
    ├── _helpers.tpl
    ├── configmap.yaml          # ASPNETCORE_ENVIRONMENT
    ├── deployment.yaml         # app
    ├── service.yaml            # app, ClusterIP
    ├── nginx-configmap.yaml    # conf reverse proxy
    ├── nginx-deployment.yaml   # nginx + sidecar exporter Prometheus
    ├── nginx-service.yaml      # nginx, seul point d'entrée
    └── NOTES.txt               # affiché après install (URL d'accès)
```

## `values.yaml`

```yaml
replicaCount: 1

image:
  repository: ghcr.io/<owner>/locatic
  tag: "latest"
  pullPolicy: IfNotPresent

service:
  type: ClusterIP
  port: 8080

config:
  aspnetcoreEnvironment: Production
  appPort: 8080

sqlite:
  existingClaim: locatic-sqlite
  dbPath: /data/locatic.db

nginx:
  enabled: true
  image: nginx:1.27-alpine
  exporterImage: nginx/nginx-prometheus-exporter:1.4
  serviceType: NodePort
```

Tout ce que le sujet demande de configurer (image, tag, replicas, env, exposition, chemin SQLite, config Nginx) passe par ce fichier, ou par `--set` / `-e` côté Ansible.

## Usage courant

```bash
helm lint infra/helm/locatic                            # vérifier la cohérence du chart
helm template locatic infra/helm/locatic | less          # relire les manifests générés
helm upgrade --install locatic infra/helm/locatic \
  -n locatic --set image.tag=<sha>                        # installer ou mettre à jour
helm status locatic -n locatic
helm history locatic -n locatic                           # releases précédentes
helm rollback locatic <revision> -n locatic               # revenir en arrière
```

## Articulation avec Ansible

Si j'utilise ce chart, il remplace les rôles `base`/`nginx` du playbook, jamais les deux en même temps (sinon deux ensembles de ressources géreraient la même application). `site.yml` appelle alors `kubernetes.core.helm` au lieu d'appliquer les templates Jinja2 (exemple complet dans [ansible.md](ansible.md#bonus-helm)).
