# ─────────────────────────────────────────────────────────────
# modules/github-marketplace/main.tf
# Provisions: Template repo, sample asset repo, teams,
#             branch protection, CODEOWNERS, Actions workflows,
#             secrets for Azure integration
# ─────────────────────────────────────────────────────────────

# ── GitHub Teams (Governance Roles) ────────────────────────

resource "github_team" "ai_platform" {
  name        = "ai-platform-engineering"
  description = "AI Platform team — operates registry, CI/CD, and marketplace infrastructure"
  privacy     = "closed"
}

resource "github_team" "ai_governance" {
  name        = "ai-governance"
  description = "AI Governance team — manages Purview policies, sensitivity labels, compliance posture"
  privacy     = "closed"
}

resource "github_team" "ai_consumers" {
  name        = "ai-consumers"
  description = "AI Consumers — discovers and integrates published AI assets into business workflows"
  privacy     = "closed"
}

# ── Template Repository ────────────────────────────────────
# The canonical template that all AI asset repos are forked from.
# Contains OASF manifest stubs, governance overlay, CODEOWNERS,
# and pre-configured GitHub Actions workflows.

resource "github_repository" "aria_asset_template" {
  name        = "aria-asset-template"
  description = "Template repository for OASF-governed AI assets (agents, skills, instructions, knowledge, orchestration)"
  visibility  = "private"

  is_template  = true
  has_issues   = true
  has_projects = false
  has_wiki     = false

  allow_squash_merge     = true
  allow_merge_commit     = false
  allow_rebase_merge     = true
  allow_auto_merge       = true
  delete_branch_on_merge = true
  vulnerability_alerts   = true

  topics = [
    "oasf", "aria", "ai-governance",
    "mcp", "agents", "enterprise-ai"
  ]
}

# ── Template: OASF Record Manifest Stub ────────────────────

resource "github_repository_file" "oasf_record_template" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "oasf-record.json"
  commit_message      = "chore: add OASF record manifest template"
  overwrite_on_create = true

  content = jsonencode({
    name           = "$${GITHUB_ORG}/agents/$${ASSET_NAME}"
    version        = "0.1.0"
    schema_version = var.oasf_schema_version
    description    = "TODO: Describe this AI asset"
    skills         = []
    domains        = []
    modules        = []
    locators = [{
      type = "source_code"
      urls = ["https://github.com/$${GITHUB_ORG}/$${REPO_NAME}"]
    }]
    authors    = ["TODO: Your Name <you@example.com>"]
    created_at = "TODO: RFC3339 timestamp"
  })
}

# ── Template: Governance Overlay Stub ──────────────────────

resource "github_repository_file" "oasf_governance_template" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "oasf-governance.json"
  commit_message      = "chore: add OASF governance overlay template"
  overwrite_on_create = true

  content = jsonencode({
    governance = {
      sensitivity_tier               = "internal"
      data_classifications           = []
      purview_label_id               = ""
      approval_chain                 = ["ai-governance"]
      allowed_consumers              = ["ai-consumers"]
      max_data_retention_days        = 90
      audit_level                    = "standard"
      dependency_sensitivity_ceiling = "confidential"
      compliance_frameworks          = []
    }
  })
}

# ── Template: CODEOWNERS ───────────────────────────────────

resource "github_repository_file" "codeowners" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = ".github/CODEOWNERS"
  commit_message      = "chore: add CODEOWNERS for governance routing"
  overwrite_on_create = true

  content = <<-EOT
    # ─────────────────────────────────────────────────────
    # CODEOWNERS — Governance-aware approval routing
    # ─────────────────────────────────────────────────────

    # OASF Record and governance overlay require governance team approval
    oasf-record.json       @${var.github_org}/ai-governance @${var.github_org}/ai-platform-engineering
    oasf-governance.json   @${var.github_org}/ai-governance

    # Workflow changes require platform team approval
    .github/workflows/     @${var.github_org}/ai-platform-engineering

    # Source code requires standard review
    src/                   @${var.github_org}/ai-platform-engineering
    tests/                 @${var.github_org}/ai-platform-engineering
  EOT
}

# ── Template: OASF Validate Workflow ───────────────────────

resource "github_repository_file" "workflow_validate" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = ".github/workflows/oasf-validate.yml"
  commit_message      = "chore: add OASF validation workflow"
  overwrite_on_create = true

  content = file("${path.module}/workflows/oasf-validate.yml")
}

# ── Template: Publish Workflow ─────────────────────────────

resource "github_repository_file" "workflow_publish" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = ".github/workflows/publish.yml"
  commit_message      = "chore: add OCI publish workflow"
  overwrite_on_create = true

  content = file("${path.module}/workflows/publish.yml")
}

# ── Template: Purview Sync Workflow ────────────────────────

resource "github_repository_file" "workflow_purview_sync" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = ".github/workflows/purview-sync.yml"
  commit_message      = "chore: add Purview sync workflow"
  overwrite_on_create = true

  content = file("${path.module}/workflows/purview-sync.yml")
}

# ── Template: Directory structure stubs ────────────────────

resource "github_repository_file" "src_readme" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "src/README.md"
  commit_message      = "chore: add source directory placeholder"
  overwrite_on_create = true
  content             = "# Source\n\nAI asset implementation goes here.\n"
}

resource "github_repository_file" "tests_readme" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "tests/README.md"
  commit_message      = "chore: add tests directory placeholder"
  overwrite_on_create = true
  content             = "# Tests\n\nOASF validation tests and evaluation suites.\n"
}

resource "github_repository_file" "docs_readme" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "docs/README.md"
  commit_message      = "chore: add docs directory placeholder"
  overwrite_on_create = true
  content             = "# Documentation\n\nUsage documentation for this AI asset.\n"
}

resource "github_repository_file" "dockerfile" {
  repository          = github_repository.aria_asset_template.name
  branch              = var.default_branch
  file                = "Dockerfile"
  commit_message      = "chore: add OCI packaging Dockerfile"
  overwrite_on_create = true
  content             = <<-DOCKER
    FROM scratch
    COPY oasf-record.json /oasf-record.json
    COPY oasf-governance.json /oasf-governance.json
    COPY src/ /src/
    COPY docs/ /docs/
  DOCKER
}

# ── Branch Protection (template repo) ──────────────────────

resource "github_branch_protection" "template_main" {
  repository_id = github_repository.aria_asset_template.node_id
  pattern       = var.default_branch

  required_pull_request_reviews {
    required_approving_review_count = 1
    require_code_owner_reviews      = true
    dismiss_stale_reviews           = true
  }

  required_status_checks {
    strict   = true
    contexts = ["validate"]
  }

  enforce_admins = false

  depends_on = [
    github_repository_file.workflow_validate
  ]
}

# ── Team Access (template repo) ────────────────────────────

resource "github_team_repository" "platform_template" {
  team_id    = github_team.ai_platform.id
  repository = github_repository.aria_asset_template.name
  permission = "admin"
}

resource "github_team_repository" "governance_template" {
  team_id    = github_team.ai_governance.id
  repository = github_repository.aria_asset_template.name
  permission = "maintain"
}

resource "github_team_repository" "consumers_template" {
  team_id    = github_team.ai_consumers.id
  repository = github_repository.aria_asset_template.name
  permission = "pull"
}

# ── Sample Agent Repository (bootstrapped from template) ───

resource "github_repository" "sample_agent" {
  count = var.enable_sample_asset ? 1 : 0

  name        = "aria-onboarding-assistant"
  description = "Sample OASF-governed agent: HR onboarding assistant with RAG and document generation"
  visibility  = "private"

  has_issues = true
  has_wiki   = false

  allow_squash_merge     = true
  allow_merge_commit     = false
  delete_branch_on_merge = true
  vulnerability_alerts   = true

  template {
    owner      = var.github_org
    repository = github_repository.aria_asset_template.name
  }

  topics = [
    "oasf", "oasf-agent", "oasf-skill-nlp-rag",
    "human-resources", "onboarding"
  ]

  depends_on = [
    github_repository_file.oasf_record_template,
    github_repository_file.oasf_governance_template,
    github_repository_file.codeowners,
    github_repository_file.workflow_validate,
  ]
}

# ── Actions Secrets (for Azure integration) ────────────────

resource "github_actions_organization_secret" "acr_login_server" {
  secret_name     = "ACR_LOGIN_SERVER"
  visibility      = "selected"
  plaintext_value = var.acr_login_server

  selected_repository_ids = compact([
    github_repository.aria_asset_template.repo_id,
    var.enable_sample_asset ? github_repository.sample_agent[0].repo_id : "",
  ])
}

resource "github_actions_organization_secret" "purview_account" {
  secret_name     = "PURVIEW_ACCOUNT"
  visibility      = "selected"
  plaintext_value = var.purview_account_name

  selected_repository_ids = compact([
    github_repository.aria_asset_template.repo_id,
    var.enable_sample_asset ? github_repository.sample_agent[0].repo_id : "",
  ])
}

resource "github_actions_organization_secret" "azure_subscription_id" {
  secret_name     = "AZURE_SUBSCRIPTION_ID"
  visibility      = "selected"
  plaintext_value = var.azure_subscription_id

  selected_repository_ids = compact([
    github_repository.aria_asset_template.repo_id,
    var.enable_sample_asset ? github_repository.sample_agent[0].repo_id : "",
  ])
}
