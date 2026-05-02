# ─────────────────────────────────────────────────────────────
# providers.tf — Provider configuration for Azure + GitHub
# ARIA Reference Implementation
# ─────────────────────────────────────────────────────────────

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    github = {
      source  = "integrations/github"
      version = "~> 6.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
  }

  # Uncomment for remote state (recommended for team use)
  # backend "azurerm" {
  #   resource_group_name  = "rg-terraform-state"
  #   storage_account_name = "sttfstateaiam"
  #   container_name       = "tfstate"
  #   key                  = "ai-asset-mgmt.tfstate"
  # }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
  subscription_id = var.azure_subscription_id
}

provider "azuread" {}

provider "github" {
  owner = var.github_org
  token = var.github_token
}
