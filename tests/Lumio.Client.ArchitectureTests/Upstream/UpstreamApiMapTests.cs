using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lumio.Client.ArchitectureTests.Upstream;

public sealed class UpstreamApiMapTests
{
    private static readonly string[] DesignAliases =
    {
        "GeneratedContract.ClientEventRecord",
        "GeneratedContract.EncodedEnvelope",
        "GeneratedContract.ConnectionCloseReason",
        "GeneratedContract.HandshakeCancelReason",
        "GeneratedContract.AuthorityReplicaUpdate",
        "GeneratedContract.CandidateGameplayCommand",
        "GeneratedContract.AuthorityPredictionUpdate",
        "RuntimeContract.CommittedPresentationDiff",
        "RuntimeContract.ReplicaApplyPlan",
        "RuntimeContract.AuthorityTransactionOutcome",
        "RuntimeContract.LocalPredictionPlan",
        "RuntimeContract.LocalPredictionOutcome",
        "RuntimeContract.PredictionReconcilePlan"
    };

    [Fact]
    public void EveryDesignAliasMapsToOnePublishedType()
    {
        var rows = LoadMap();
        Assert.Equal(DesignAliases.OrderBy(a => a), rows.Select(r => r.Alias).OrderBy(a => a));
        var byAlias = new Dictionary<string, MapRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            Assert.False(byAlias.ContainsKey(row.Alias), "duplicate alias " + row.Alias);
            byAlias[row.Alias] = row;
            if (row.Status == "published")
            {
                Assert.False(string.IsNullOrWhiteSpace(row.PublishedType));
                Assert.DoesNotContain("GeneratedContract.", row.PublishedType, StringComparison.Ordinal);
                Assert.DoesNotContain("RuntimeContract.", row.PublishedType, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal("blocked-unpublished", row.Status);
                Assert.True(string.IsNullOrEmpty(row.PublishedType));
                Assert.False(string.IsNullOrWhiteSpace(row.BlockId));
                Assert.False(string.IsNullOrWhiteSpace(row.Reason));
            }
        }
    }

    [Fact]
    public void GeneratedFixtureCorpusIsVersionPinned()
    {
        var catalogPath = System.IO.Path.Combine(RepoRoot.Path, "tests", "Fixtures", "index.json");
        Assert.True(File.Exists(catalogPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var pin = doc.RootElement.GetProperty("upstreamCorpusPin");
        Assert.Equal("UPSTREAM-GENERATED-CONTRACT-API-MAP", pin.GetProperty("requirement").GetString());
        Assert.True(pin.TryGetProperty("status", out _));
        Assert.True(pin.TryGetProperty("hashes", out var hashes));
        Assert.Equal(JsonValueKind.Array, hashes.ValueKind);
        if (pin.GetProperty("status").GetString() == "unpublished")
        {
            Assert.Equal(0, hashes.GetArrayLength());
        }
    }

    [Fact]
    public void NoClientDefinedEnvelopeOrTransactionContract()
    {
        foreach (var file in Directory.EnumerateFiles(System.IO.Path.Combine(RepoRoot.Path, "modules"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("struct Envelope", text, StringComparison.Ordinal);
            Assert.DoesNotContain("class Envelope", text, StringComparison.Ordinal);
            Assert.DoesNotContain("record Envelope", text, StringComparison.Ordinal);
            Assert.DoesNotContain("struct Transaction", text, StringComparison.Ordinal);
            Assert.DoesNotContain("class Transaction", text, StringComparison.Ordinal);
            Assert.DoesNotContain("record Transaction", text, StringComparison.Ordinal);
            Assert.DoesNotContain("struct ErrorCode", text, StringComparison.Ordinal);
            Assert.DoesNotContain("class ErrorCode", text, StringComparison.Ordinal);
            Assert.DoesNotContain("enum ErrorCode", text, StringComparison.Ordinal);
        }
    }

    private static MapRow[] LoadMap()
    {
        var md = File.ReadAllText(System.IO.Path.Combine(RepoRoot.Path, "eng", "upstream-api-map.md"));
        var json = Regex.Match(md, "```json\\r?\\n([\\s\\S]*?)```").Groups[1].Value;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("aliases").EnumerateArray().Select(e => new MapRow(
            e.GetProperty("alias").GetString()!,
            e.GetProperty("status").GetString()!,
            e.GetProperty("publishedType").ValueKind == JsonValueKind.Null ? null : e.GetProperty("publishedType").GetString(),
            e.GetProperty("blockId").GetString(),
            e.GetProperty("reason").GetString())).ToArray();
    }

    private sealed record MapRow(string Alias, string Status, string? PublishedType, string? BlockId, string? Reason);
}
