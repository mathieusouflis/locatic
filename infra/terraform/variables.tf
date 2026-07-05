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
  type        = string
  description = <<-EOT
    StorageClass du PVC SQLite. Défaut "csi-hostpath-sc" (addon minikube
    csi-hostpath-driver) : contrairement au hostPath par défaut, ce driver CSI
    expose les métriques kubelet_volume_stats_* nécessaires au panneau Grafana
    "PVC : % utilisé" et à l'alerte SqlitePvcAlmostFull.
    Mettre "standard" pour revenir au hostPath (pas de métriques de volume).
  EOT
  default     = "csi-hostpath-sc"
}
