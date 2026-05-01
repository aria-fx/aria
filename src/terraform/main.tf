# ─────────────────────────────────────────────────────────────
# main.tf — Root orchestration
# ARIA Reference Implementation
# ─────────────────────────────────────────────────────────────

data "azurerm_client_config" "current" {}
data "azuread_client_config" "current" {}

locals {
  resource_prefix = "${var.project_prefix}-${var.environment}"
  common_tags = merge(var.tags, {
    environment = var.environment
  })
}

# ─────────────────────────────────────────────────────────────
# Layer 1: Azure Governance Infrastructure
# ─────────────────────────────────────────────────────────────

module "azure_governance" {
  source = "./modules/azure-governance"

  resource_prefix          = local.resource_prefix
  location                 = var.azure_location
  tags                     = local.common_tags
  tenant_id                = data.azurerm_client_config.current.tenant_id
  deployer_object_id       = data.azurerm_client_config.current.object_id
  enable_private_endpoints = var.enable_private_endpoints
  sensitivity_tiers        = var.sensitivity_tiers
}

# ─────────────────────────────────────────────────────────────
# Layer 2: GitHub Marketplace Infrastructure
# ─────────────────────────────────────────────────────────────

module "github_marketplace" {
  source = "./modules/github-marketplace"

  github_org             = var.github_org
  default_branch         = var.github_default_branch
  oasf_schema_version    = var.oasf_schema_version
  enable_sample_asset    = var.enable_sample_asset_repo
  sensitivity_tiers      = var.sensitivity_tiers

  # Azure integration secrets for GitHub Actions
  azure_subscription_id  = var.azure_subscription_id
  azure_tenant_id        = data.azurerm_client_config.current.tenant_id
  acr_login_server       = module.oasf_validation.acr_login_server
  acr_name               = module.oasf_validation.acr_name
  purview_account_name   = module.azure_governance.purview_account_name
  purview_endpoint       = module.azure_governance.purview_catalog_endpoint
  key_vault_name         = module.azure_governance.key_vault_name
}

# ─────────────────────────────────────────────────────────────
# Layer 3: OASF Validation & OCI Registry
# ─────────────────────────────────────────────────────────────

module "oasf_validation" {
  source = "./modules/oasf-validation"

  resource_prefix    = local.resource_prefix
  location           = var.azure_location
  tags               = local.common_tags
  resource_group_id  = module.azure_governance.resource_group_id
  resource_group_name = module.azure_governance.resource_group_name
  purview_identity_id = module.azure_governance.purview_identity_principal_id
  oasf_schema_version = var.oasf_schema_version
}
