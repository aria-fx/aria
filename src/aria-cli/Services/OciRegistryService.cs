// ─────────────────────────────────────────────────────────────
// Services/OciRegistryService.cs
// Handles OCI artifact operations: pull manifests, fetch OASF
// records and governance overlays from OCI registries.
// ─────────────────────────────────────────────────────────────

using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;

namespace Aria.Cli.Services;

/// <summary>
/// Abstracts OCI registry operations for ARIA.
/// In production, this uses Azure.Containers.ContainerRegistry
/// or oras-go for registry interactions. For the prototype,
/// it supports local file-based resolution for demo purposes.
/// </summary>
public sealed class OciRegistryService : IGovernanceOverlayResolver
{
    private readonly Func<RegistrySearchRequest, CancellationToken, Task<RegistrySearchResponse>> _registrySearchClient;

    public OciRegistryService()
        : this(DefaultRegistrySearchClientAsync)
    {
    }

    public OciRegistryService(Func<RegistrySearchRequest, CancellationToken, Task<RegistrySearchResponse>> registrySearchClient)
    {
        _registrySearchClient = registrySearchClient;
    }

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
        var result = await FetchGovernanceWithStateAsync(ociReference);
        return result.Overlay;
    }

    public async Task<GovernanceOverlayFetchResult> FetchGovernanceWithStateAsync(string ociReference)
    {
        var localPath = ResolveLocalPath(ociReference, "oasf-governance.json");
        if (localPath == null || !File.Exists(localPath))
            return new GovernanceOverlayFetchResult(null, RegistryGovernanceStates.Ungoverned, null);

        try
        {
            var json = await File.ReadAllTextAsync(localPath);
            var overlay = JsonSerializer.Deserialize<OasfGovernanceOverlay>(json);
            return overlay != null
                ? new GovernanceOverlayFetchResult(overlay, RegistryGovernanceStates.Governed, null)
                : new GovernanceOverlayFetchResult(null, RegistryGovernanceStates.GovernanceUnreachable, "Failed to deserialize governance overlay.");
        }
        catch (Exception ex)
        {
            return new GovernanceOverlayFetchResult(null, RegistryGovernanceStates.GovernanceUnreachable, ex.Message);
        }
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
    public async Task<List<OasfRecord>> SearchAsync(
        string? skill = null,
        string? domain = null,
        string? keyword = null,
        IEnumerable<string>? registries = null)
    {
        var result = await SearchDetailedAsync(skill, domain, keyword, registries);
        return result.Records.ToList();
    }

    public async Task<RegistrySearchAggregateResult> SearchDetailedAsync(
        string? skill = null,
        string? domain = null,
        string? keyword = null,
        IEnumerable<string>? registries = null,
        IDictionary<string, RegistrySourcePolicyConfig>? registryPolicies = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRegistries = registries?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (normalizedRegistries.Count == 0)
            return new RegistrySearchAggregateResult([], [], []);

        var discoveryTasks = normalizedRegistries.Select(async registry =>
        {
            try
            {
                var response = await _registrySearchClient(new RegistrySearchRequest(registry, skill, domain, keyword), cancellationToken);
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new RegistrySearchResponse(registry, Array.Empty<RegistrySearchAsset>(), ex.Message);
            }
        });

        var responses = await Task.WhenAll(discoveryTasks);
        var policyMap = normalizedRegistries.ToDictionary(
            r => r,
            r => registryPolicies != null && registryPolicies.TryGetValue(r, out var configured)
                ? configured
                : new RegistrySourcePolicyConfig(),
            StringComparer.OrdinalIgnoreCase);
        var diagnostics = responses
            .Select(r => new RegistrySearchDiagnostic(r.Registry, r.Error, r.Assets.Count))
            .ToList();

        var selectedAssets = responses
            .Where(r => r.Error == null)
            .SelectMany(r => r.Assets.Select(asset => new RegistrySearchAssetCandidate(
                asset.Record,
                r.Registry,
                asset.GovernanceState,
                asset.GovernanceError,
                policyMap[r.Registry])))
            .GroupBy(c => CanonicalAssetKey(c.Record), StringComparer.OrdinalIgnoreCase)
            .Select(SelectPreferredCandidate)
            .OrderBy(a => a.Record.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Record.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => CanonicalRefKey(a.Record), StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Record.Description, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RegistrySearchAggregateResult(
            selectedAssets.Select(a => a.Record).ToList(),
            diagnostics,
            selectedAssets);
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

    private static string CanonicalAssetKey(OasfRecord record) =>
        $"{record.Name}|{record.Version}|{CanonicalRefKey(record)}";

    private static RegistrySearchAssetCandidate SelectPreferredCandidate(IGrouping<string, RegistrySearchAssetCandidate> candidates)
    {
        var ordered = candidates
            .OrderByDescending(c => c.Policy.Priority)
            .ThenBy(c => c.Registry, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.GovernanceState, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var winner = ordered[0];
        var tiedWinners = ordered.Where(c => c.Policy.Priority == winner.Policy.Priority).ToList();
        if (tiedWinners.Count == 1)
            return winner with { ResolutionReason = $"Selected by highest priority ({winner.Policy.Priority})." };

        var tieBreakWinner = tiedWinners
            .OrderBy(c => c.Registry, StringComparer.OrdinalIgnoreCase)
            .First();
        return tieBreakWinner with
        {
            IsAmbiguous = true,
            ResolutionReason = $"Ambiguous priorities ({winner.Policy.Priority}) across: {string.Join(", ", tiedWinners.Select(t => t.Registry))}. Explicit source selection required."
        };
    }

    private static string CanonicalRefKey(OasfRecord record) =>
        record.Modules
            .Select(m => m.Ref)
            .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))
        ?? record.Locators
            .SelectMany(l => l.Urls)
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
        ?? string.Empty;

    private static bool MatchesFilters(OasfRecord record, string? skill, string? domain, string? keyword)
    {
        if (!string.IsNullOrWhiteSpace(skill) &&
            !record.Skills.Any(s => s.Name.Contains(skill, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(domain) &&
            !record.Domains.Any(d => d.Name.Contains(domain, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var matchesKeyword =
                record.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                record.Skills.Any(s => s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                record.Domains.Any(d => d.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (!matchesKeyword)
                return false;
        }

        return true;
    }

    private static async Task<RegistrySearchResponse> DefaultRegistrySearchClientAsync(RegistrySearchRequest request, CancellationToken cancellationToken)
    {
        var registryPath = ResolveRegistryPath(request.Registry);
        if (registryPath == null || !Directory.Exists(registryPath))
            return new RegistrySearchResponse(request.Registry, Array.Empty<RegistrySearchAsset>(), $"Registry unavailable or unsupported: {request.Registry}");

        var results = new List<RegistrySearchAsset>();
        var recordFiles = Directory.GetFiles(registryPath, "oasf-record.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in recordFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var record = JsonSerializer.Deserialize<OasfRecord>(json);
            if (record == null)
                continue;

            if (MatchesFilters(record, request.Skill, request.Domain, request.Keyword))
            {
                var governancePath = Path.Combine(Path.GetDirectoryName(filePath)!, "oasf-governance.json");
                if (!File.Exists(governancePath))
                {
                    results.Add(new RegistrySearchAsset(record, RegistryGovernanceStates.Ungoverned));
                    continue;
                }

                try
                {
                    var governanceJson = await File.ReadAllTextAsync(governancePath, cancellationToken);
                    _ = JsonSerializer.Deserialize<OasfGovernanceOverlay>(governanceJson);
                    results.Add(new RegistrySearchAsset(record, RegistryGovernanceStates.Governed));
                }
                catch (Exception ex)
                {
                    results.Add(new RegistrySearchAsset(record, RegistryGovernanceStates.GovernanceUnreachable, ex.Message));
                }
            }
        }

        return new RegistrySearchResponse(request.Registry, results, null);
    }

    private static string? ResolveRegistryPath(string registry)
    {
        if (string.IsNullOrWhiteSpace(registry))
            return null;

        if (Uri.TryCreate(registry, UriKind.Absolute, out var uri) && uri.IsFile)
            return uri.LocalPath;

        if (Directory.Exists(registry))
            return registry;

        return null;
    }
}

public sealed record RegistrySearchRequest(string Registry, string? Skill, string? Domain, string? Keyword);

public sealed record GovernanceOverlayFetchResult(OasfGovernanceOverlay? Overlay, string GovernanceState, string? Error);

public sealed record RegistrySearchAsset(OasfRecord Record, string GovernanceState, string? GovernanceError = null);

public sealed record RegistrySearchResponse(string Registry, IReadOnlyList<RegistrySearchAsset> Assets, string? Error)
{
    public RegistrySearchResponse(string registry, IReadOnlyList<OasfRecord> records, string? error)
        : this(
            registry,
            records
                .Select(r => new RegistrySearchAsset(r, RegistryGovernanceStates.Ungoverned))
                .ToList(),
            error)
    {
    }
}

public sealed record RegistrySearchDiagnostic(string Registry, string? Error, int ResultCount);

public sealed record RegistrySearchAggregateResult(
    IReadOnlyList<OasfRecord> Records,
    IReadOnlyList<RegistrySearchDiagnostic> Diagnostics,
    IReadOnlyList<RegistrySearchAssetCandidate> SelectedAssets);

public sealed record RegistrySearchAssetCandidate(
    OasfRecord Record,
    string Registry,
    string GovernanceState,
    string? GovernanceError,
    RegistrySourcePolicyConfig Policy,
    bool IsAmbiguous = false,
    string? ResolutionReason = null);
