# ─────────────────────────────────────────────────────────────
# modules/github-marketplace/outputs.tf
# ─────────────────────────────────────────────────────────────

output "template_repo_url" {
  value = github_repository.aria_asset_template.html_url
}

output "sample_agent_repo_url" {
  value = var.enable_sample_asset ? github_repository.sample_agent[0].html_url : "not created"
}

output "team_ids" {
  value = {
    ai_platform   = github_team.ai_platform.id
    ai_governance  = github_team.ai_governance.id
    ai_consumers   = github_team.ai_consumers.id
  }
}

output "template_repo_name" {
  value = github_repository.aria_asset_template.name
}
