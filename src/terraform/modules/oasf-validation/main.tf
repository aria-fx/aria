# ─────────────────────────────────────────────────────────────
# modules/oasf-validation/main.tf
# Provisions: Azure Container Registry for OCI artifact storage,
#             role assignments for Purview scanning, storage
#             for OASF schema validation artifacts
# ─────────────────────────────────────────────────────────────

# ── Azure Container Registry ──────────────────────────────
# OCI-compliant registry for storing AI asset artifacts.
# OASF Records are packaged as OCI artifacts and pushed here
# by the publish GitHub Action. Content-addressed via digest.

resource "azurerm_container_registry" "oasf" {
  name                = replace("acr${var.resource_prefix}oasf", "-", "")
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Standard"
  admin_enabled       = true

  tags = var.tags
}

# ── RBAC: Purview → ACR (scan OCI artifacts) ──────────────
# Allows Purview to scan the container registry and catalog
# AI asset artifacts in the Purview Data Map.

resource "azurerm_role_assignment" "purview_acr_reader" {
  scope                = azurerm_container_registry.oasf.id
  role_definition_name = "AcrPull"
  principal_id         = var.purview_identity_id
}

# ── Storage: OASF Schema Snapshots ────────────────────────
# Stores copies of the OASF schema versions used for validation,
# ensuring reproducibility and audit trail for schema compliance.

resource "azurerm_storage_account" "oasf_schemas" {
  name                     = replace("st${var.resource_prefix}oasf", "-", "")
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  tags = var.tags
}

resource "azurerm_storage_container" "schema_versions" {
  name                  = "oasf-schema-versions"
  storage_account_id    = azurerm_storage_account.oasf_schemas.id
  container_access_type = "private"
}

# ── Seed: Upload current OASF schema version reference ────

resource "azurerm_storage_blob" "current_schema_version" {
  name                   = "schema-${var.oasf_schema_version}.json"
  storage_account_name   = azurerm_storage_account.oasf_schemas.name
  storage_container_name = azurerm_storage_container.schema_versions.name
  type                   = "Block"

  source_content = jsonencode({
    oasf_schema_version = var.oasf_schema_version
    validated_at        = timestamp()
    required_record_fields = [
      "name", "version", "schema_version",
      "skills", "locators", "authors", "created_at"
    ]
    required_governance_fields = [
      "sensitivity_tier", "approval_chain",
      "audit_level"
    ]
    supported_module_types = [
      "mcp_server", "prompt_bundle", "knowledge_base",
      "orchestration_config", "evaluation_metrics",
      "feature_flags"
    ]
  })
}
