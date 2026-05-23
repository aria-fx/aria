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

# ── Skill Lifecycle ────────────────────────────────────────

variable "aria_cross_repo_token" {
  description = "PAT with repo and issues scopes for cross-repository workflow operations (drift detection, orchestration)"
  type        = string
  sensitive   = true
}

variable "openai_api_key" {
  description = "OpenAI API key for skill eval pipelines (gpt-4o, gpt-4o-mini)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "anthropic_api_key" {
  description = "Anthropic API key for skill eval pipelines (claude-sonnet-4, claude-haiku-4-5)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "azure_openai_api_key" {
  description = "Azure OpenAI API key for skill eval pipelines (azure-gpt-4o)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "azure_openai_endpoint" {
  description = "Azure OpenAI endpoint URL for skill eval pipelines"
  type        = string
  sensitive   = true
  default     = ""
}

variable "eval_pass_threshold" {
  description = "Minimum eval pass-rate percentage required for the quality gate (0–100)"
  type        = number
  default     = 80
}

variable "drift_threshold" {
  description = "Maximum percentage-point drop in pass rate before drift is flagged"
  type        = number
  default     = 3
}
