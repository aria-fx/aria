// ─────────────────────────────────────────────────────────────
// Services/OciRegistryService.cs
// Handles OCI artifact operations: pull manifests, fetch OASF
// records and governance overlays from OCI registries.
// ─────────────────────────────────────────────────────────────

using System.Text.Json;
using Aria.Cli.Models;

namespace Aria.Cli.Services;

/// <summary>
/// Abstracts OCI registry operations for ARIA.
/// In production, this uses Azure.Containers.ContainerRegistry
/// or oras-go for registry interactions. For the prototype,
/// it supports local file-based resolution for demo purposes.
/// </summary>
public sealed class OciRegistryService
{
    /// <summary>
    /// Fetch the OASF Record from an OCI artifact reference.
    /// </summary>
    public async Task<OasfRecord?> FetchRecordAsync(string ociReference)
    {
        // Try local resolution first (for demo/testing)
        var localPath = ResolveLocalPath(ociReference, "oasf-record.json");
        if (localPath != null && File.Exists(localPath))
        {
            var json = await File.ReadAllTextAsync(localPath);
            return JsonSerializer.Deserialize<OasfRecord>(json);
        }

        // Production: use Azure.Containers.ContainerRegistry
        // var client = new ContainerRegistryClient(new Uri($"https://{registry}"), credential);
        // var manifest = await client.GetManifestAsync(repo, tag);
        // ... extract oasf-record.json layer

        return null;
    }

    /// <summary>
    /// Fetch the Governance Overlay from an OCI artifact reference.
    /// </summary>
    public async Task<OasfGovernanceOverlay?> FetchGovernanceAsync(string ociReference)
    {
        var localPath = ResolveLocalPath(ociReference, "oasf-governance.json");
        if (localPath != null && File.Exists(localPath))
        {
            var json = await File.ReadAllTextAsync(localPath);
            return JsonSerializer.Deserialize<OasfGovernanceOverlay>(json);
        }

        return null;
    }

    /// <summary>
    /// Pull the full OCI artifact to a local directory.
    /// </summary>
    public async Task<string?> PullArtifactAsync(string ociReference, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        // Copy all artifact files to output directory
        var localBase = ResolveLocalBase(ociReference);
        if (localBase != null && Directory.Exists(localBase))
        {
            foreach (var file in Directory.GetFiles(localBase, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(localBase, file);
                var destPath = Path.Combine(outputDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, overwrite: true);
            }
            return outputDir;
        }

        // Production: oras pull $ociReference -o $outputDir
        return null;
    }

    /// <summary>
    /// Search registries for assets matching OASF skill/domain criteria.
    /// </summary>
    public Task<List<OasfRecord>> SearchAsync(
        string? skill = null,
        string? domain = null,
        string? keyword = null,
        IEnumerable<string>? registries = null)
    {
        // Production: query Agent Directory Service (ADS) or
        // GitHub API with topic-based filtering
        // GET /orgs/{org}/packages?package_type=container&q=oasf-skill-{skill}

        // Demo: return sample results
        var results = new List<OasfRecord>
        {
            new()
            {
                Name = "aria.dev/agents/onboarding-assistant",
                Version = "2.1.0",
                SchemaVersion = "1.0.0",
                Description = "HR onboarding agent with policy Q&A and document generation",
                Skills = [new(30101, "knowledge_retrieval/rag"), new(10201, "nlp/nlg/text_completion")],
                Domains = [new("human_resources/onboarding")],
                Authors = ["Josh Garverick <josh.garverick@aria.dev>"]
            },
            new()
            {
                Name = "aria.dev/skills/policy-lookup",
                Version = "1.0.0",
                SchemaVersion = "1.0.0",
                Description = "MCP server for HR policy knowledge base lookup",
                Skills = [new(30101, "knowledge_retrieval/rag")],
                Domains = [new("human_resources")],
                Modules = [new() { Type = "mcp_server", Transport = "stdio" }],
                Authors = ["Josh Garverick <josh.garverick@aria.dev>"]
            },
            new()
            {
                Name = "aria.dev/knowledge/hr-policies",
                Version = "3.0.0",
                SchemaVersion = "1.0.0",
                Description = "HR policy knowledge base with PTO, benefits, and compensation data",
                Skills = [new(30101, "knowledge_retrieval/rag")],
                Domains = [new("human_resources")],
                Modules = [new() { Type = "knowledge_base" }],
                Authors = ["HR Team <hr@aria.dev>"]
            }
        };

        // Filter by skill
        if (skill != null)
            results = results.Where(r =>
                r.Skills.Any(s => s.Name.Contains(skill, StringComparison.OrdinalIgnoreCase))).ToList();

        // Filter by domain
        if (domain != null)
            results = results.Where(r =>
                r.Domains.Any(d => d.Name.Contains(domain, StringComparison.OrdinalIgnoreCase))).ToList();

        return Task.FromResult(results);
    }

    private static string? ResolveLocalPath(string ociReference, string fileName)
    {
        var basePath = ResolveLocalBase(ociReference);
        return basePath != null ? Path.Combine(basePath, fileName) : null;
    }

    private static string? ResolveLocalBase(string ociReference)
    {
        // For local testing: map OCI refs to local directories
        // e.g., ghcr.io/jgarverick/aria-assets/onboarding-assistant:2.1.0
        //     → ./demo-assets/onboarding-assistant/
        var parts = ociReference.Split('/');
        var nameAndTag = parts.LastOrDefault()?.Split(':');
        var assetName = nameAndTag?.FirstOrDefault();

        if (assetName != null)
        {
            var candidate = Path.Combine("demo-assets", assetName);
            if (Directory.Exists(candidate)) return candidate;
        }

        return null;
    }
}
