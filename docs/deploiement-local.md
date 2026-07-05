# Déploiement local

Ordre exact des actions sur mon poste une fois qu'une image est publiée par la CI (merge sur `main`).

## Prérequis (une seule fois)

```bash
brew install minikube kubectl terraform ansible helm   # macOS
pip install kubernetes                                  # module Python requis par kubernetes.core
cd infra/ansible && ansible-galaxy collection install -r requirements.yml
```

> **Docker Desktop : désactiver le magasin d'images containerd.**
> Si `minikube start` reste bloqué indéfiniment sur `Pulling base image` /
> `Loading kicbase ... from local cache` (aucun conteneur `minikube` créé), c'est
> que Docker Desktop utilise le magasin d'images containerd (driver `overlayfs`).
> Le driver `docker` de minikube attend le magasin classique `overlay2`.
> Correction : Docker Desktop → Settings → General → décocher **« Use containerd
> for pulling and storing images »** → Apply & restart. Vérifier ensuite :
>
> ```bash
> docker info --format '{{.Driver}}'   # doit afficher : overlay2
> ```

## Étape 0 : récupérer le tag de l'image

Chaque merge sur `main` publie `ghcr.io/<owner>/locatic:<sha>` et `:latest` (voir [ci-cd.md](ci-cd.md)).

```bash
git checkout main && git pull
IMAGE_TAG=$(git rev-parse HEAD)
```

Si le package GHCR est privé : soit imagePullSecret côté cluster, soit rendre le package public (Settings du package > Change visibility).

### Variante : construire l'image en local (sans CI / sans GHCR)

Pour déployer une version locale sans attendre la CI, on construit l'image
**directement dans le daemon Docker de minikube**. Comme le tag n'est pas `latest`,
Kubernetes utilise l'image locale (`imagePullPolicy: IfNotPresent`, voir défaut du
rôle `base`) sans jamais contacter GHCR.

```bash
minikube start                                  # lancer le cluster minikube
eval $(minikube docker-env)                     # changer de shell sur celui dans minikube
docker build --target production \
  -t ghcr.io/mathieusouflis/locatic:manual \
  -f app/Dockerfile app/
eval $(minikube docker-env -u)                  # remet le shell sur le Docker du pc
export IMAGE_TAG=manual                         # exporte la variable d'environnement pour le tag
```

> Ne pas déployer avec `app_tag=latest` **et** une image seulement locale : sur le
> tag `latest`, k8s force `imagePullPolicy=Always` et tente un pull GHCR qui échoue
> (`ImagePullBackOff` / `manifest unknown`). Utiliser un tag dédié (`manual`, un SHA…).

## Étape 1 : démarrer minikube

```bash
minikube start
minikube status          # host / kubelet / apiserver doivent tous être Running

minikube addons enable volumesnapshots
minikube addons enable csi-hostpath-driver
```

> Sans l'addon `csi-hostpath-driver`, le PVC reste `Pending` (aucun provisioner
> pour `csi-hostpath-sc`). Pour rester sur le hostPath par défaut (sans métriques
> de volume), déployer avec `terraform apply -var sqlite_storage_class=standard`.

## Étape 2 : Terraform prépare l'infrastructure

```bash
cd infra/terraform
terraform init
terraform plan
terraform apply
terraform output
```

Détails et gestion de l'état : [terraform.md](terraform.md).

## Étape 3 : Ansible déploie l'application

```bash
cd ../ansible
ansible-playbook site.yml -i inventory.yml -e app_tag=$IMAGE_TAG -e discord_webhook_url="URL" # Discord webhook url pour les notifications
```

Le playbook vérifie d'abord les prérequis (`bootstrap-checks.yml`), lit les outputs Terraform, applique les manifests app + Nginx (rôles `base`/`nginx`), attend que le déploiement soit prêt. Détails dans [ansible.md](ansible.md).

## Étape 4 : vérifier

```bash
kubectl get all -n locatic # tout doit etre "Running"
```

### Accès web : redirection de ports (port-forward)

Méthode par défaut, sans configuration réseau ni `sudo`. Un script redirige
les deux services d'un coup :

```bash
minikube service -n locatic nginx --url
# Ctrl+C pour tout arrêter.
```

Ouvrir ensuite <http://localhost:le-port-affiche-dans-le-terminal> (l'app) et <http://localhost:le-port-affiche-dans-le-terminal> (Grafana).
Vérification rapide en ligne de commande, script lancé dans un autre terminal :

```bash
curl -s http://localhost:le-port-affiche-dans-le-terminal/health           # doit répondre Healthy, via Nginx
```

## Étape 5 : le monitoring

```bash
minikube service -n monitoring monitoring-grafana --url
```

Détails dans [monitoring.md](monitoring.md).

## Mettre à jour vers une nouvelle version

```bash
ansible-playbook site.yml -i inventory.yml -e app_tag=<nouveau-sha> -e discord_webhook_url="URL" # Discord webhook url pour les notifications
```

Pas besoin de rejouer Terraform : namespace et PVC existent déjà, les données SQLite sont conservées.

## Revenir en arrière (rollback)

Chaque image est épinglée par son SHA de commit :

```bash
ansible-playbook site.yml -i inventory.yml -e app_tag=<sha-precedent>  -e discord_webhook_url="URL" # Discord webhook url pour les notifications
# ou directement :
kubectl rollout undo deployment/locatic -n locatic
```
