# ─────────────────────────────────────────────────────────────
# modules/oasf-validation/outputs.tf
# ─────────────────────────────────────────────────────────────

output "acr_login_server" {
  value = azurerm_container_registry.oasf.login_server
}

output "acr_name" {
  value = azurerm_container_registry.oasf.name
}

output "acr_id" {
  value = azurerm_container_registry.oasf.id
}

output "schema_storage_account" {
  value = azurerm_storage_account.oasf_schemas.name
}
