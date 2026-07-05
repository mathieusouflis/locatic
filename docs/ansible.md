# Ansible

## Différence avec l'exemple vu en cours

Même structure de playbook : un `site.yml` qui importe un playbook de vérification des prérequis puis applique deux rôles (`base`, `nginx`) à un groupe d'hôtes. Mais le contenu change beaucoup. En cours, Ansible configure des hôtes distants (conteneurs Docker, `ansible_connection: docker`) avec des modules classiques (`apk`, `copy`, `command nginx -s reload`).

Ici il n'y a pas de machine distante à configurer : tout tourne comme pods dans un cluster Kubernetes local. Ansible pilote `kubectl` depuis mon poste (`ansible_connection: local`), et mes rôles appliquent des ressources Kubernetes via `kubernetes.core.k8s` plutôt que d'installer des paquets.

## Ce que fait le playbook

1. `bootstrap-checks.yml` vérifie que `kubectl` est disponible et que minikube est démarré.
2. le rôle **base** lit les outputs Terraform (namespace, PVC SQLite) et déploie l'application (Deployment + Service).
3. le rôle **nginx** déploie la config Nginx (ConfigMap) puis Nginx (Deployment + Service) en reverse proxy.

## Organisation des fichiers (`infra/ansible/`)

```
infra/ansible/
├── site.yml                       # playbook principal
├── bootstrap-checks.yml           # vérifie kubectl / minikube
├── inventory.yml                  # un seul groupe "local" → localhost
├── requirements.yml                # collection kubernetes.core
├── group_vars/all.yml             # terraform_dir, app_port (partagés entre rôles)
└── roles/
    ├── base/
    │   ├── defaults/main.yml
    │   ├── tasks/main.yml         # lit les outputs TF, déploie l'app
    │   └── templates/
    │       ├── app-deployment.yaml.j2
    │       └── app-service.yaml.j2
    └── nginx/
        ├── defaults/main.yml
        ├── tasks/main.yml         # déploie la conf + Nginx
        ├── handlers/main.yml      # "reload nginx" → rollout restart
        └── templates/
            ├── nginx-configmap.yaml.j2
            ├── nginx-deployment.yaml.j2
            └── nginx-service.yaml.j2
```

`inventory.yml` : un seul groupe local, variables de déploiement au niveau `vars` :

```yaml
"all":
  "children":
    "local":
      "hosts":
        "localhost":
          "ansible_connection": "local"
  "vars":
    "app_name": "locatic"
    "app_image": "ghcr.io/<owner>/locatic"
    "app_tag": "latest"
    "app_replicas": 1
    "kube_context": "minikube"
```

`group_vars/all.yml` contient les variables partagées entre rôles. Les `defaults` d'un rôle ne sont visibles que par ce rôle ; comme `app_port` sert à la fois à `base` et à `nginx`, il est dans `group_vars/all.yml` :

```yaml
terraform_dir: "{{ playbook_dir }}/../terraform"
app_port: 8080
```

## Templates

Chaque rôle a son propre dossier `templates/`. `app-deployment.yaml.j2` et `app-service.yaml.j2` (rôle `base`) reçoivent `app_image`, `app_tag`, `app_replicas`, `namespace`, `sqlite_pvc` ; ceux du rôle `nginx` reçoivent `namespace`, `app_name`, `app_port`. Je peux changer d'image ou de tag sans toucher au YAML, et les valeurs venant de Terraform ne sont jamais recopiées à la main.

## Installer les prérequis

```bash
pip install ansible kubernetes
cd infra/ansible
ansible-galaxy collection install -r requirements.yml
```

## Lancer le déploiement

```bash
cd infra/ansible
ansible-playbook site.yml -i inventory.yml --syntax-check   # vérifie la syntax des fichiers ansible
ansible-playbook site.yml -i inventory.yml                  # déploie le playbook
ansible-playbook site.yml -i inventory.yml -e app_tag=<sha> # utilise une image précise pour le déploiement
```

## Le bonus Helm

Si le chart Helm est en place (voir [helm.md](helm.md)), les rôles `base` et `nginx` peuvent être remplacés par un seul appel `kubernetes.core.helm` :

```yaml
- name: Déployer la release Helm
  kubernetes.core.helm:
    name: locatic
    chart_ref: "{{ playbook_dir }}/../helm/locatic"
    release_namespace: "{{ namespace }}"
    create_namespace: false
    values:
      image: { repository: "{{ app_image }}", tag: "{{ app_tag }}" }
      sqlite: { existingClaim: "{{ sqlite_pvc }}" }
```

C'est une alternative aux rôles `base`/`nginx`, pas un ajout : jamais les deux en même temps, les noms de ressources générés diffèrent d'une méthode à l'autre.
