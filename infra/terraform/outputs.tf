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
