# ─────────────────────────────────────────────────────────────
# environments/dev/terraform.tfvars
# Development environment configuration
# ─────────────────────────────────────────────────────────────

# ── Azure ──────────────────────────────────────────────────
azure_subscription_id = "00000000-0000-0000-0000-000000000000" # Replace with your subscription
azure_location        = "eastus2"
environment           = "dev"
project_prefix        = "aria"

tags = {
  project     = "aria"
  framework   = "oasf"
  managed_by  = "terraform"
  cost_center = "engineering"
}

# ── GitHub ─────────────────────────────────────────────────
github_org             = "your-org"  # Replace with your GitHub org
github_default_branch  = "main"

# ── OASF ───────────────────────────────────────────────────
oasf_schema_version = "1.0.0"

sensitivity_tiers = [
  "public",
  "internal",
  "confidential",
  "highly_confidential",
  "restricted"
]

# ── Feature Flags ──────────────────────────────────────────
enable_private_endpoints = false  # Enable for production
enable_sample_asset_repo = true
