# Terraform

## Ce que Terraform fait, et ce qu'il ne fait pas

Le sujet est clair : Terraform *prépare* l'infrastructure locale, Ansible s'occupe du *déploiement* applicatif. Terraform est donc limité à deux ressources :

- le **namespace** Kubernetes de l'application (`locatic`) ;
- le **volume persistant** (PersistentVolumeClaim) pour SQLite.

Terraform expose ces informations en *outputs*, qu'Ansible lit pour savoir où déployer (voir [ansible.md](ansible.md)). Avantage concret : le namespace et le volume changent presque jamais, alors que l'application est redéployée à chaque nouvelle image. `terraform apply` ne touche donc jamais à l'application.

## Les fichiers (`infra/terraform/`)

`main.tf` déclare le provider et les deux ressources :

```hcl
terraform {
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.38"
    }
  }
}

provider "kubernetes" {
  config_path    = "~/.kube/config"
  config_context = var.kube_context
}

resource "kubernetes_namespace_v1" "main" {
  metadata {
    name = var.namespace
    labels = {
      project    = "locatic"
      managed-by = "terraform"
    }
  }
}

resource "kubernetes_persistent_volume_claim_v1" "sqlite" {
  metadata {
    name      = "locatic-sqlite"
    namespace = kubernetes_namespace_v1.main.metadata[0].name
    labels    = { app = var.app_name }
  }
  wait_until_bound = false
  spec {
    access_modes       = ["ReadWriteOnce"]
    storage_class_name = var.sqlite_storage_class
    resources {
      requests = { storage = var.sqlite_storage }
    }
  }
}
```

Pas besoin de déclarer le `PersistentVolume` : la StorageClass provisionne le volume dès que le PVC est créé. Par défaut on utilise `csi-hostpath-sc` (addon minikube `csi-hostpath-driver`) plutôt que le `standard` hostPath, car seul un driver CSI expose les métriques `kubelet_volume_stats_*` du panneau Grafana « PVC : % utilisé ». Repasser à `standard` avec `-var sqlite_storage_class=standard`.

`variables.tf` :

```hcl
variable "kubeconfig_path" {
  type    = string
  default = "~/.kube/config"
}

variable "kube_context" {
  type        = string
  description = "Contexte kubectl à utiliser, ici minikube."
  default     = "minikube"
}

variable "namespace" {
  type    = string
  default = "locatic"
}

variable "app_name" {
  type    = string
  default = "locatic"
}

variable "sqlite_storage" {
  type    = string
  default = "256Mi"
}

variable "sqlite_storage_class" {
  type    = string
  default = "csi-hostpath-sc"   # "standard" = sans métriques de volume
}
```

`outputs.tf`, le contrat entre Terraform et Ansible :

```hcl
output "namespace" {
  value = kubernetes_namespace_v1.main.metadata[0].name
}

output "sqlite_pvc_name" {
  value = kubernetes_persistent_volume_claim_v1.sqlite.metadata[0].name
}

output "runtime_contract" {
  value = {
    namespace       = kubernetes_namespace_v1.main.metadata[0].name
    sqlite_pvc_name = kubernetes_persistent_volume_claim_v1.sqlite.metadata[0].name
    app_name        = var.app_name
  }
}
```

Pour changer une valeur (taille du volume par exemple), créer `terraform.tfvars` (jamais commité, dans `.gitignore`) à partir de `infra/terraform/terraform.tfvars.example`, lui versionné.

## Exécution

```bash
minikube start                 # le cluster minikube doit exister avant d'apply la config terraform
cd infra/terraform

terraform init                 # télécharge le provider k8s
terraform fmt -check
terraform validate
terraform plan                 # montre les ressources qui vont être crées / edit
terraform apply                # crée le namespace + le PVC

terraform output               # valeurs qu'Ansible va utiliser
```

Vérification :

```bash
kubectl get namespace locatic
kubectl get pvc -n locatic     # locatic-sqlite : Bound (ou Pending tant qu'aucun pod ne l'utilise)
```

## L'état Terraform

`terraform.tfstate` reste uniquement en local, jamais versionné (peut contenir des données sensibles, et n'aurait de toute façon aucun sens sur une autre machine). `.gitignore` exclut `*.tfstate*`, `.terraform/` et `*.tfvars`. `terraform destroy` supprime le namespace et les données, puis `apply` recrée tout.

## Enchaînement avec Ansible

Ansible lit ces outputs via `terraform output -json` (voir [ansible.md](ansible.md)). Un changement de nom de namespace ou de PVC côté Terraform est récupéré automatiquement au déploiement suivant.
