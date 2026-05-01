# ─────────────────────────────────────────────────────────────
# outputs.tf — Key outputs for reference and downstream use
# ─────────────────────────────────────────────────────────────

# ── Azure Governance ───────────────────────────────────────

output "resource_group_name" {
  description = "Name of the governance resource group"
  value       = module.azure_governance.resource_group_name
}

output "purview_account_name" {
  description = "Microsoft Purview account name"
  value       = module.azure_governance.purview_account_name
}

output "purview_catalog_endpoint" {
  description = "Purview catalog API endpoint"
  value       = module.azure_governance.purview_catalog_endpoint
}

output "purview_identity_principal_id" {
  description = "Purview managed identity principal ID (for RBAC grants)"
  value       = module.azure_governance.purview_identity_principal_id
}

output "key_vault_uri" {
  description = "Key Vault URI for governance secrets"
  value       = module.azure_governance.key_vault_uri
}

output "lineage_storage_account" {
  description = "Storage account for OASF lineage metadata"
  value       = module.azure_governance.storage_account_name
}

# ── OCI Registry ───────────────────────────────────────────

output "acr_login_server" {
  description = "Azure Container Registry login server for OCI artifacts"
  value       = module.oasf_validation.acr_login_server
}

output "acr_name" {
  description = "Azure Container Registry name"
  value       = module.oasf_validation.acr_name
}

# ── GitHub Marketplace ─────────────────────────────────────

output "template_repo_url" {
  description = "URL of the AI asset template repository"
  value       = module.github_marketplace.template_repo_url
}

output "sample_agent_repo_url" {
  description = "URL of the sample agent repository"
  value       = module.github_marketplace.sample_agent_repo_url
}

output "team_ids" {
  description = "GitHub team IDs for governance roles"
  value       = module.github_marketplace.team_ids
}
