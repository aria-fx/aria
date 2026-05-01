# ─────────────────────────────────────────────────────────────
# variables.tf — Input variables
# ─────────────────────────────────────────────────────────────

# ── Azure ──────────────────────────────────────────────────

variable "azure_subscription_id" {
  description = "Azure subscription ID for governance resources"
  type        = string
}

variable "azure_location" {
  description = "Azure region for all resources"
  type        = string
  default     = "eastus2"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "project_prefix" {
  description = "Naming prefix for all resources"
  type        = string
  default     = "aria"
}

variable "tags" {
  description = "Common tags applied to all Azure resources"
  type        = map(string)
  default = {
    project    = "aria"
    framework  = "oasf"
    managed_by = "terraform"
  }
}

# ── GitHub ─────────────────────────────────────────────────

variable "github_org" {
  description = "GitHub organization name"
  type        = string
}

variable "github_token" {
  description = "GitHub PAT with admin:org, repo, workflow scopes"
  type        = string
  sensitive   = true
}

variable "github_default_branch" {
  description = "Default branch name for new repositories"
  type        = string
  default     = "main"
}

# ── OASF ───────────────────────────────────────────────────

variable "oasf_schema_version" {
  description = "OASF schema version for validation"
  type        = string
  default     = "1.0.0"
}

variable "sensitivity_tiers" {
  description = "Ordered sensitivity tiers for label inheritance"
  type        = list(string)
  default     = ["public", "internal", "confidential", "highly_confidential", "restricted"]
}

# ── Feature Flags ──────────────────────────────────────────

variable "enable_private_endpoints" {
  description = "Enable private endpoints for Purview and Key Vault (production)"
  type        = bool
  default     = false
}

variable "enable_sample_asset_repo" {
  description = "Create a sample AI asset repo bootstrapped from the template"
  type        = bool
  default     = true
}
