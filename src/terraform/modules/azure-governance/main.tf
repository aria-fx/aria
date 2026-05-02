# ─────────────────────────────────────────────────────────────
# modules/azure-governance/main.tf
# Provisions: Resource Group, Purview Account, Key Vault,
#             Storage Account, Managed Identity, Role Assignments
# ─────────────────────────────────────────────────────────────

# ── Resource Group ─────────────────────────────────────────

resource "azurerm_resource_group" "governance" {
  name     = "rg-${var.resource_prefix}-governance"
  location = var.location
  tags     = var.tags
}

# ── Microsoft Purview Account ──────────────────────────────
# The governance backbone — data classification, sensitivity
# labels, DLP, lineage, and AI interaction auditing.

resource "azurerm_purview_account" "main" {
  name                = "purview-${var.resource_prefix}"
  resource_group_name = azurerm_resource_group.governance.name
  location            = azurerm_resource_group.governance.location

  identity {
    type = "SystemAssigned"
  }

  public_network_enabled       = !var.enable_private_endpoints
  managed_resource_group_name  = "rg-${var.resource_prefix}-purview-managed"

  tags = var.tags
}

# ── Key Vault ──────────────────────────────────────────────
# Stores OASF governance secrets: sensitivity tier mappings,
# approval chain configs, Purview API credentials for CI/CD.

resource "azurerm_key_vault" "governance" {
  name                       = "kv-${var.resource_prefix}"
  location                   = azurerm_resource_group.governance.location
  resource_group_name        = azurerm_resource_group.governance.name
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 30
  purge_protection_enabled   = true
  enable_rbac_authorization  = true

  tags = var.tags
}

# ── Storage Account ────────────────────────────────────────
# Stores OASF lineage metadata exports, governance overlay
# snapshots, and audit trail archives.

resource "azurerm_storage_account" "lineage" {
  name                     = replace("st${var.resource_prefix}lineage", "-", "")
  resource_group_name      = azurerm_resource_group.governance.name
  location                 = azurerm_resource_group.governance.location
  account_tier             = "Standard"
  account_replication_type = "GRS"
  min_tls_version          = "TLS1_2"

  blob_properties {
    versioning_enabled = true
  }

  tags = var.tags
}

resource "azurerm_storage_container" "oasf_records" {
  name                  = "oasf-records"
  storage_account_id    = azurerm_storage_account.lineage.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "lineage_exports" {
  name                  = "lineage-exports"
  storage_account_id    = azurerm_storage_account.lineage.id
  container_access_type = "private"
}

resource "azurerm_storage_container" "governance_overlays" {
  name                  = "governance-overlays"
  storage_account_id    = azurerm_storage_account.lineage.id
  container_access_type = "private"
}

# ── RBAC: Purview → Storage (scan lineage data) ───────────

resource "azurerm_role_assignment" "purview_storage_reader" {
  scope                = azurerm_storage_account.lineage.id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_purview_account.main.identity[0].principal_id
}

# ── RBAC: Deployer → Key Vault (manage secrets) ──────────

resource "azurerm_role_assignment" "deployer_kv_admin" {
  scope                = azurerm_key_vault.governance.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = var.deployer_object_id
}

# ── RBAC: Purview → Key Vault (read scanning credentials) ─

resource "azurerm_role_assignment" "purview_kv_reader" {
  scope                = azurerm_key_vault.governance.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_purview_account.main.identity[0].principal_id
}

# ── Key Vault Secrets: Sensitivity Tier Configuration ─────
# Stores the ordered sensitivity tiers as a JSON array so
# the OASF validation Action can enforce ceiling checks.

resource "azurerm_key_vault_secret" "sensitivity_tiers" {
  name         = "oasf-sensitivity-tiers"
  value        = jsonencode(var.sensitivity_tiers)
  key_vault_id = azurerm_key_vault.governance.id

  depends_on = [azurerm_role_assignment.deployer_kv_admin]
}

# ── Key Vault Secret: Purview Endpoint ────────────────────

resource "azurerm_key_vault_secret" "purview_endpoint" {
  name         = "purview-catalog-endpoint"
  value        = "https://${azurerm_purview_account.main.name}.purview.azure.com"
  key_vault_id = azurerm_key_vault.governance.id

  depends_on = [azurerm_role_assignment.deployer_kv_admin]
}
