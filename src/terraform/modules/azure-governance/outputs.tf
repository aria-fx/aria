# ─────────────────────────────────────────────────────────────
# modules/azure-governance/outputs.tf
# ─────────────────────────────────────────────────────────────

output "resource_group_id" {
  value = azurerm_resource_group.governance.id
}

output "resource_group_name" {
  value = azurerm_resource_group.governance.name
}

output "purview_account_name" {
  value = azurerm_purview_account.main.name
}

output "purview_catalog_endpoint" {
  value = "https://${azurerm_purview_account.main.name}.purview.azure.com"
}

output "purview_identity_principal_id" {
  value = azurerm_purview_account.main.identity[0].principal_id
}

output "key_vault_name" {
  value = azurerm_key_vault.governance.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.governance.vault_uri
}

output "storage_account_name" {
  value = azurerm_storage_account.lineage.name
}

output "storage_account_id" {
  value = azurerm_storage_account.lineage.id
}
