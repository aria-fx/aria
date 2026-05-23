# ─────────────────────────────────────────────────────────────
# modules/github-marketplace/variables.tf
# ─────────────────────────────────────────────────────────────

variable "github_org" {
  type = string
}

variable "default_branch" {
  type    = string
  default = "main"
}

variable "oasf_schema_version" {
  type    = string
  default = "1.0.0"
}

variable "enable_sample_asset" {
  type    = bool
  default = true
}

variable "sensitivity_tiers" {
  type = list(string)
}

# Azure integration variables (passed from root)
variable "azure_subscription_id" {
  type = string
}

variable "azure_tenant_id" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "acr_name" {
  type = string
}

variable "purview_account_name" {
  type = string
}

variable "purview_endpoint" {
  type = string
}

variable "key_vault_name" {
  type = string
}

# ── Skill Lifecycle — Cross-Repo Token ─────────────────────

variable "aria_cross_repo_token" {
  description = "PAT with repo and issues scopes for cross-repository workflow operations (drift detection, orchestration)"
  type        = string
  sensitive   = true
}

# ── Skill Lifecycle — Provider API Keys (aria-skills) ──────

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

# ── Skill Lifecycle — Eval Thresholds (aria-skills) ────────

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

# ── Skill Lifecycle — Repo Names ───────────────────────────

variable "skills_repo_name" {
  description = "Name of the aria-skills repository within the GitHub organization"
  type        = string
  default     = "aria-skills"
}

variable "gateway_repo_name" {
  description = "Name of the aria-gateway repository within the GitHub organization"
  type        = string
  default     = "aria-gateway"
}

variable "aria_repo_name" {
  description = "Name of the aria (orchestration) repository within the GitHub organization"
  type        = string
  default     = "aria"
}
