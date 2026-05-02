# 02 — Marketplace Setup

Deploy the ARIA infrastructure on Azure and GitHub using Terraform.

## What you'll build

By the end of this module you'll have:

- A **Microsoft Purview Account** with system-assigned managed identity
- An **Azure Key Vault** storing sensitivity tier configuration
- A **Storage Account** for OASF lineage metadata and governance overlays
- An **Azure Container Registry** for OCI artifact storage
- A **GitHub template repository** with OASF manifests, CODEOWNERS, and three Actions workflows
- A **sample agent repository** bootstrapped from the template
- Three **GitHub teams** (AI Platform, AI Governance, AI Consumers) with scoped permissions
- **Branch protection** requiring OASF validation to pass before merge

## Prerequisites

- Azure subscription with Owner or Contributor + User Access Administrator
- GitHub organization with admin access
- Terraform >= 1.5.0 (installed by devcontainer)
- Azure CLI authenticated (`az login`)
- GitHub Personal Access Token with `admin:org`, `repo`, `workflow` scopes

## Step 1: Configure your environment

Copy the example tfvars and fill in your values:

```bash
cd src/terraform/environments/dev
cp terraform.tfvars terraform.tfvars.local
```

Edit `terraform.tfvars.local`:

```hcl
azure_subscription_id = "your-subscription-id-here"
azure_location        = "eastus2"
environment           = "dev"
project_prefix        = "aria"

github_org            = "your-github-org"
github_default_branch = "main"
```

Set your GitHub token as an environment variable (never commit this):

```bash
export TF_VAR_github_token="ghp_your_token_here"
```

## Step 2: Initialize Terraform

```bash
cd src/terraform/environments/dev
terraform init
```

You should see providers downloading: `azurerm`, `github`, `azuread`.

## Step 3: Review the plan

```bash
terraform plan -var-file=terraform.tfvars.local -out=plan.tfplan
```

Review the output. You should see approximately 30 resources being created
across three modules:

| Module | Resources | Count |
|--------|-----------|-------|
| azure-governance | Resource group, Purview, Key Vault, Storage, RBAC | ~10 |
| github-marketplace | Repos, teams, branch protection, workflows, secrets | ~15 |
| oasf-validation | Container Registry, schema storage, RBAC | ~5 |

## Step 4: Apply

```bash
terraform apply plan.tfplan
```

This takes 5–10 minutes. The longest resource is the Purview Account
(typically 3–5 minutes to provision).

## Step 5: Verify the deployment

Check the outputs:

```bash
terraform output
```

You should see:

```
acr_login_server       = "acrariadevaoasf.azurecr.io"
purview_endpoint       = "https://purview-aria-dev.purview.azure.com"
template_repo          = "https://github.com/your-org/aria-asset-template"
sample_repo            = "https://github.com/your-org/aria-onboarding-assistant"
```

Verify each component:

```bash
# Azure resources
az group show -n rg-aria-dev-governance -o table
az purview account show -n purview-aria-dev -g rg-aria-dev-governance -o table

# GitHub repos
gh repo view your-org/aria-asset-template --json name,description
gh repo view your-org/aria-onboarding-assistant --json name,description

# GitHub teams
gh api orgs/your-org/teams --jq '.[].name'
```

## Step 6: Inspect the template repository

Open the template repo in your browser. You should see:

```
aria-asset-template/
├── oasf-record.json          ← OASF Record stub (needs customization)
├── oasf-governance.json      ← Governance overlay stub
├── src/README.md
├── tests/README.md
├── docs/README.md
├── Dockerfile                ← OCI packaging
└── .github/
    ├── CODEOWNERS            ← Routes to ai-governance and ai-platform teams
    └── workflows/
        ├── oasf-validate.yml ← Runs on PR: schema + governance + ceiling check
        ├── publish.yml       ← Runs on merge: build + push OCI artifact
        └── purview-sync.yml  ← Runs post-publish: apply Purview labels + lineage
```

The branch protection on `main` requires:

- The `validate` status check to pass (OASF schema validation)
- At least one CODEOWNERS approval
- Stale reviews dismissed on new pushes

## Understanding the modules

### azure-governance

This module provisions the governance backbone. The Purview Account gets a
system-assigned managed identity that's granted `Storage Blob Data Reader`
on the lineage storage account and `Key Vault Secrets User` on the Key Vault.
This allows Purview to scan data sources and read governance configuration
without any stored credentials.

The Key Vault stores two secrets:

- `oasf-sensitivity-tiers` — JSON array of the ordered sensitivity tiers
- `purview-catalog-endpoint` — the Purview API endpoint URL

### github-marketplace

This module creates the template repository with all OASF manifest stubs
and workflows pre-configured. The CODEOWNERS file routes governance overlay
changes to the `ai-governance` team and workflow changes to `ai-platform`.

Organization-level Actions secrets (`ACR_LOGIN_SERVER`, `PURVIEW_ACCOUNT`,
`AZURE_SUBSCRIPTION_ID`) are created and scoped to the template and sample
repos so the CI/CD workflows can authenticate to Azure.

### oasf-validation

This module provisions the Azure Container Registry where published AI
assets land as OCI artifacts. Purview is granted `AcrPull` access so it
can scan the registry and catalog AI assets in the Data Map.

A schema version reference blob is seeded into the schema storage account,
defining required fields for both the OASF Record and governance overlay.

## Cleanup

To tear down the entire environment:

```bash
terraform destroy -var-file=terraform.tfvars.local
```

This removes all Azure resources and GitHub repos/teams. Use with caution
in shared environments.

## Next Steps

→ [03 — Your First Asset](./03-first-asset.md)
