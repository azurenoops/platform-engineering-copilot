# ==============================================================================
# Outputs for Azure Kubernetes Service Module
# ==============================================================================

output "aks_id" {
  description = "ID of the AKS cluster"
  value       = azurerm_kubernetes_cluster.aks.id
}

output "aks_name" {
  description = "Name of the AKS cluster"
  value       = azurerm_kubernetes_cluster.aks.name
}

output "aks_fqdn" {
  description = "FQDN of the AKS cluster"
  value       = azurerm_kubernetes_cluster.aks.fqdn
}

output "aks_private_fqdn" {
  description = "Private FQDN of the AKS cluster"
  value       = azurerm_kubernetes_cluster.aks.private_fqdn
}

output "aks_node_resource_group" {
  description = "Resource group for AKS nodes"
  value       = azurerm_kubernetes_cluster.aks.node_resource_group
}

output "aks_kubelet_identity_object_id" {
  description = "Object ID of the kubelet identity"
  value       = azurerm_kubernetes_cluster.aks.kubelet_identity[0].object_id
}

output "aks_kubelet_identity_client_id" {
  description = "Client ID of the kubelet identity"
  value       = azurerm_kubernetes_cluster.aks.kubelet_identity[0].client_id
}

output "aks_oidc_issuer_url" {
  description = "OIDC issuer URL for workload identity"
  value       = azurerm_kubernetes_cluster.aks.oidc_issuer_url
}

output "aks_identity_principal_id" {
  description = "Principal ID of the AKS managed identity"
  value       = azurerm_kubernetes_cluster.aks.identity[0].principal_id
}

output "aks_identity_tenant_id" {
  description = "Tenant ID of the AKS managed identity"
  value       = azurerm_kubernetes_cluster.aks.identity[0].tenant_id
}

output "kube_config" {
  description = "Kubernetes configuration for kubectl"
  value       = azurerm_kubernetes_cluster.aks.kube_config_raw
  sensitive   = true
}

output "kube_admin_config" {
  description = "Kubernetes admin configuration"
  value       = azurerm_kubernetes_cluster.aks.kube_admin_config_raw
  sensitive   = true
}
