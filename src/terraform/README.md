# ARIA — Terraform Reference Implementation

This Terraform configuration provisions the complete infrastructure for the
ARIA reference architecture described in the
companion document and conference deck.

## Architecture Layers Provisioned

| Layer | Resources | Provider |
|-------|-----------|----------|
| **Governance** | Purview Account, Key Vault, Storage (lineage), Role Assignments, Managed Identity | `azurerm` |
| **Marketplace** | GitHub Org repos (template + sample assets), Branch Protection, CODEOWNERS, Actions Workflows, Secrets, Teams | `github` |
| **Validation** | Container Registry (GHCR/ACR), OASF validation Action, purview-sync Action | `azurerm` + `github` |

## Directory Structure

```
terraform-ref-impl/
├── main.tf                      # Root orchestration — calls all modules
├── variables.tf                 # Input variables (subscription, org, naming)
├── outputs.tf                   # Key outputs (Purview endpoint, repo URLs, etc.)
├── providers.tf                 # Provider configuration (azurerm, github)
├── modules/
│   ├── azure-governance/        # Purview, Key Vault, Storage, RBAC
│   ├── github-marketplace/      # Repos, teams, branch protection, workflows
│   └── oasf-validation/         # ACR, OASF schema validation resources
├── workflows/                   # GitHub Actions workflow templates
│   ├── oasf-validate.yml
│   ├── publish.yml
│   └── purview-sync.yml
└── environments/
    └── dev/                     # Dev environment tfvars
```

## Prerequisites

- Azure subscription with Owner or Contributor + User Access Administrator
- GitHub organization with admin access
- Terraform >= 1.5.0
- Azure CLI authenticated (`az login`)
- GitHub Personal Access Token with `admin:org`, `repo`, `workflow` scopes

## Quick Start

```bash
cd environments/dev
terraform init
terraform plan -out=plan.tfplan
terraform apply plan.tfplan
```

## What Gets Created

### Azure
- Resource Group for AI governance
- Microsoft Purview Account with System-Assigned Managed Identity
- Azure Key Vault for OASF governance secrets
- Storage Account for lineage metadata and OASF artifacts
- Azure Container Registry for OCI artifact storage
- Role assignments granting Purview access to scan storage and ACR
- Private DNS zones (optional, for production hardening)

### GitHub
- AI Asset template repository with standardized structure
- Sample agent repository bootstrapped from template
- GitHub Teams (AI Platform, AI Governance, AI Consumers)
- Branch protection rules enforcing OASF validation
- GitHub Actions secrets for Azure integration
- CODEOWNERS file mapping governance roles
- Pre-configured workflow files for OASF validate, publish, and Purview sync
