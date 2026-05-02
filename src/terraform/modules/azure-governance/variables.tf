# ─────────────────────────────────────────────────────────────
# modules/azure-governance/variables.tf
# ─────────────────────────────────────────────────────────────

variable "resource_prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "tags" {
  type = map(string)
}

variable "tenant_id" {
  type = string
}

variable "deployer_object_id" {
  type = string
}

variable "enable_private_endpoints" {
  type    = bool
  default = false
}

variable "sensitivity_tiers" {
  type    = list(string)
  default = ["public", "internal", "confidential", "highly_confidential", "restricted"]
}
