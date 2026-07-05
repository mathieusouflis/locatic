terraform {
  required_version = ">= 1.5"
  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.35"
    }
  }
}

provider "kubernetes" {
  config_path    = pathexpand(var.kubeconfig_path)
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
    labels = {
      app = var.app_name
    }
  }

  wait_until_bound = false

  spec {
    access_modes       = ["ReadWriteOnce"]
    storage_class_name = var.sqlite_storage_class
    resources {
      requests = {
        storage = var.sqlite_storage
      }
    }
  }
}
