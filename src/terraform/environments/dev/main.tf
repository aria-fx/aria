# ─────────────────────────────────────────────────────────────
# environments/dev/main.tf
# Thin wrapper that calls the root module with dev-specific vars
# ─────────────────────────────────────────────────────────────

module "aria_asset_management" {
  source = "../../"

  azure_subscription_id    = var.azure_subscription_id
  azure_location           = var.azure_location
  environment              = var.environment
  project_prefix           = var.project_prefix
  tags                     = var.tags
  github_org               = var.github_org
  github_token             = var.github_token
  github_default_branch    = var.github_default_branch
  oasf_schema_version      = var.oasf_schema_version
  sensitivity_tiers        = var.sensitivity_tiers
  enable_private_endpoints = var.enable_private_endpoints
  enable_sample_asset_repo = var.enable_sample_asset_repo
}

# Re-export key outputs
output "purview_endpoint" {
  value = module.aria_asset_management.purview_catalog_endpoint
}

output "acr_server" {
  value = module.aria_asset_management.acr_login_server
}

output "template_repo" {
  value = module.aria_asset_management.template_repo_url
}

output "sample_repo" {
  value = module.aria_asset_management.sample_agent_repo_url
}

# ── Variables (pass-through) ───────────────────────────────

variable "azure_subscription_id" { type = string }
variable "azure_location" { type = string }
variable "environment" { type = string }
variable "project_prefix" { type = string }
variable "tags" { type = map(string) }
variable "github_org" { type = string }
variable "github_token" { type = string; sensitive = true }
variable "github_default_branch" { type = string }
variable "oasf_schema_version" { type = string }
variable "sensitivity_tiers" { type = list(string) }
variable "enable_private_endpoints" { type = bool }
variable "enable_sample_asset_repo" { type = bool }
