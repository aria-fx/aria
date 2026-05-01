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
