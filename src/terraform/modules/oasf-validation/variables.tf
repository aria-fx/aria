# ─────────────────────────────────────────────────────────────
# modules/oasf-validation/variables.tf
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

variable "resource_group_id" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "purview_identity_id" {
  type = string
}

variable "oasf_schema_version" {
  type    = string
  default = "1.0.0"
}
