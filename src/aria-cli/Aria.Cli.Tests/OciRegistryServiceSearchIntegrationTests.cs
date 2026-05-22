using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class OciRegistryServiceSearchIntegrationTests
{
    [Fact]
    public async Task SearchDetailedAsync_QueriesAllConfiguredRegistries_AndPropagatesFilters()
    {
        var calls = new List<RegistrySearchRequest>();
        var service = new OciRegistryService((request, _) =>
        {
            calls.Add(request);
            return Task.FromResult(new RegistrySearchResponse(request.Registry, Array.Empty<RegistrySearchAsset>(), null));
        });

        var config = new AriaConfig
        {
            Registries = ["registry-a", "registry-b"]
        };

        await service.SearchDetailedAsync("knowledge_retrieval/rag", "human_resources", "policy", config.Registries);

        Assert.Equal(2, calls.Count);
        Assert.Equal("registry-a", calls[0].Registry);
        Assert.Equal("registry-b", calls[1].Registry);
        Assert.All(calls, call =>
        {
            Assert.Equal("knowledge_retrieval/rag", call.Skill);
            Assert.Equal("human_resources", call.Domain);
            Assert.Equal("policy", call.Keyword);
        });
    }

    [Fact]
    public async Task SearchDetailedAsync_AggregatesDeduplicatesAndKeepsDeterministicOrdering()
    {
        var service = new OciRegistryService((request, _) =>
        {
            List<RegistrySearchAsset> records = request.Registry switch
            {
                "registry-a" => [
                    new RegistrySearchAsset(CreateRecord("zeta/asset", "1.0.0", "oci://zeta@sha256:a"), RegistryGovernanceStates.Governed),
                    new RegistrySearchAsset(CreateRecord("alpha/asset", "1.0.0", "oci://alpha@sha256:a"), RegistryGovernanceStates.Governed)
                ],
                "registry-b" => [
                    new RegistrySearchAsset(CreateRecord("alpha/asset", "1.0.0", "oci://alpha@sha256:a"), RegistryGovernanceStates.Ungoverned),
                    new RegistrySearchAsset(CreateRecord("beta/asset", "2.0.0", "oci://beta@sha256:b"), RegistryGovernanceStates.Ungoverned)
                ],
                _ => []
            };

            return Task.FromResult(new RegistrySearchResponse(request.Registry, records, null));
        });

        var first = await service.SearchDetailedAsync(registries: ["registry-a", "registry-b"]);
        var second = await service.SearchDetailedAsync(registries: ["registry-a", "registry-b"]);

        Assert.Equal(3, first.Records.Count);
        Assert.Equal(
            ["alpha/asset:1.0.0", "beta/asset:2.0.0", "zeta/asset:1.0.0"],
            first.Records.Select(r => $"{r.Name}:{r.Version}").ToArray());
        Assert.Equal(
            first.Records.Select(r => $"{r.Name}:{r.Version}"),
            second.Records.Select(r => $"{r.Name}:{r.Version}"));
    }

    [Fact]
    public async Task SearchDetailedAsync_DuplicateAcrossRegistries_SelectsHighestPrioritySource()
    {
        var service = new OciRegistryService((request, _) =>
        {
            List<RegistrySearchAsset> assets =
            [
                new RegistrySearchAsset(CreateRecord("alpha/asset", "1.0.0", "oci://alpha@sha256:a"), RegistryGovernanceStates.Governed)
            ];
            return Task.FromResult(new RegistrySearchResponse(request.Registry, assets, null));
        });

        var result = await service.SearchDetailedAsync(
            registries: ["registry-low", "registry-high"],
            registryPolicies: new Dictionary<string, RegistrySourcePolicyConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["registry-low"] = new() { Priority = 5, TrustTier = "public_curated" },
                ["registry-high"] = new() { Priority = 50, TrustTier = "internal_governed" }
            });

        var selected = Assert.Single(result.SelectedAssets);
        Assert.Equal("registry-high", selected.Registry);
        Assert.False(selected.IsAmbiguous);
        Assert.Equal("governed", selected.GovernanceState);
    }

    [Fact]
    public async Task SearchDetailedAsync_DuplicatePriorityTie_MarksSelectionAsAmbiguous()
    {
        var service = new OciRegistryService((request, _) =>
        {
            List<RegistrySearchAsset> assets =
            [
                new RegistrySearchAsset(CreateRecord("alpha/asset", "1.0.0", "oci://alpha@sha256:a"), RegistryGovernanceStates.Ungoverned)
            ];
            return Task.FromResult(new RegistrySearchResponse(request.Registry, assets, null));
        });

        var result = await service.SearchDetailedAsync(
            registries: ["registry-a", "registry-b"],
            registryPolicies: new Dictionary<string, RegistrySourcePolicyConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["registry-a"] = new() { Priority = 10 },
                ["registry-b"] = new() { Priority = 10 }
            });

        var selected = Assert.Single(result.SelectedAssets);
        Assert.True(selected.IsAmbiguous);
        Assert.Contains("Explicit source selection required", selected.ResolutionReason);
    }

    [Fact]
    public async Task SearchDetailedAsync_PartialFailure_ReturnsOtherRegistryResultsAndDiagnostics()
    {
        var service = new OciRegistryService((request, _) =>
        {
            if (request.Registry == "registry-down")
                throw new InvalidOperationException("timeout");

            return Task.FromResult(new RegistrySearchResponse(
                request.Registry,
                [CreateRecord("gamma/asset", "1.2.3", "oci://gamma@sha256:c")],
                null));
        });

        var result = await service.SearchDetailedAsync(registries: ["registry-down", "registry-up"]);

        Assert.Single(result.Records);
        Assert.Equal("gamma/asset", result.Records[0].Name);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, d => d.Registry == "registry-down" && d.Error != null);
        Assert.Contains(result.Diagnostics, d => d.Registry == "registry-up" && d.Error == null);
    }

    [Fact]
    public async Task SearchDetailedAsync_PropagatesGovernanceUnreachableState()
    {
        var service = new OciRegistryService((request, _) =>
        {
            var assets = new List<RegistrySearchAsset>
            {
                new(CreateRecord("gamma/asset", "1.2.3", "oci://gamma@sha256:c"), RegistryGovernanceStates.GovernanceUnreachable, "timeout")
            };
            return Task.FromResult(new RegistrySearchResponse(request.Registry, assets, null));
        });

        var result = await service.SearchDetailedAsync(registries: ["registry-a"]);

        var selected = Assert.Single(result.SelectedAssets);
        Assert.Equal(RegistryGovernanceStates.GovernanceUnreachable, selected.GovernanceState);
        Assert.Equal("timeout", selected.GovernanceError);
    }

    [Fact]
    public async Task SearchDetailedAsync_EmptyRegistryList_ReturnsNoResultsAndNoDiagnostics()
    {
        var queried = false;
        var service = new OciRegistryService((request, _) =>
        {
            queried = true;
            return Task.FromResult(new RegistrySearchResponse(request.Registry, Array.Empty<RegistrySearchAsset>(), null));
        });

        var result = await service.SearchDetailedAsync(registries: []);

        Assert.Empty(result.Records);
        Assert.Empty(result.Diagnostics);
        Assert.False(queried);
    }

    [Fact]
    public async Task SearchAsync_FileRegistries_UsesRealDiscoveryAndAppliesFilters()
    {
        var root = Path.Combine(Path.GetTempPath(), "aria-search-integration-" + Guid.NewGuid().ToString("N"));
        var registryA = Path.Combine(root, "registry-a");
        var registryB = Path.Combine(root, "registry-b");
        Directory.CreateDirectory(registryA);
        Directory.CreateDirectory(registryB);

        try
        {
            await WriteRecordAsync(registryA, "asset-a", CreateRecord("aria.dev/skills/policy-lookup", "1.0.0", "oci://policy@sha256:1"));
            await WriteRecordAsync(registryB, "asset-b", CreateRecord("aria.dev/skills/policy-lookup", "1.0.0", "oci://policy@sha256:1"));
            await WriteRecordAsync(registryB, "asset-c", CreateRecord("aria.dev/agents/onboarding-assistant", "2.1.0", "oci://onboarding@sha256:2", description: "onboarding helper"));

            var service = new OciRegistryService();
            var results = await service.SearchAsync(
                skill: "knowledge_retrieval/rag",
                domain: "human_resources",
                keyword: "policy",
                registries: [registryA, registryB]);

            Assert.Single(results);
            Assert.Equal("aria.dev/skills/policy-lookup", results[0].Name);
            Assert.Equal("1.0.0", results[0].Version);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static OasfRecord CreateRecord(string name, string version, string moduleRef, string description = "policy search asset") =>
        new()
        {
            Name = name,
            Version = version,
            SchemaVersion = "1.0.0",
            Description = description,
            Skills = [new OasfSkill(30101, "knowledge_retrieval/rag")],
            Domains = [new OasfDomain("human_resources")],
            Modules = [new OasfModule { Type = "mcp_server", Ref = moduleRef, Transport = "stdio" }],
            Authors = ["ARIA Team <team@aria.dev>"]
        };

    private static async Task WriteRecordAsync(string registryPath, string assetDirName, OasfRecord record)
    {
        var assetPath = Path.Combine(registryPath, assetDirName);
        Directory.CreateDirectory(assetPath);
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(assetPath, "oasf-record.json"), json);
    }
}
