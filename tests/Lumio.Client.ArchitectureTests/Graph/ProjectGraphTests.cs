using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Graph;

public sealed class ProjectGraphTests
{
    private static readonly string[] Eleven =
    {
        "Lumio.Client.Session",
        "Lumio.Client.Connection",
        "Lumio.Client.Handshake",
        "Lumio.Client.Replica",
        "Lumio.Client.Prediction",
        "Lumio.Client.Input",
        "Lumio.Client.Persistence",
        "Lumio.Client.Observability",
        "Lumio.Client.UnityAdapter",
        "Lumio.Client.HybridClrAdapter",
        "Lumio.Client.Bot"
    };

    [Fact]
    public void AllElevenModuleAssembliesExist()
    {
        foreach (var assembly in Eleven)
        {
            var matches = Directory.EnumerateFiles(RepoRoot.Path, assembly + ".csproj", SearchOption.AllDirectories)
                .Where(p => p.Contains($"{System.IO.Path.DirectorySeparatorChar}src{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || p.Contains("/src/", StringComparison.Ordinal))
                .ToArray();
            Assert.True(matches.Length == 1, assembly + " production csproj");
        }

        Assert.True(File.Exists(System.IO.Path.Combine(RepoRoot.Path, "LumioClient.slnx")));
        Assert.True(File.Exists(System.IO.Path.Combine(RepoRoot.Path, "modules", "bot", "host", "Lumio.Client.Bot.Host.csproj")));
        Assert.True(File.Exists(System.IO.Path.Combine(RepoRoot.Path, "tests", "Lumio.Client.ArchitectureTests", "Lumio.Client.ArchitectureTests.csproj")));
        Assert.True(File.Exists(System.IO.Path.Combine(RepoRoot.Path, "tests", "Lumio.Client.IntegrationTests", "Lumio.Client.IntegrationTests.csproj")));
    }

    [Fact]
    public void ProjectReferencesAreAllowlisted()
    {
        var allow = Allowlist.Load();
        foreach (var (from, tos) in CsprojGraph.ProductionEdges())
        {
            Assert.True(allow.TryGetValue(from, out var allowedFrom), "allowlist missing " + from);
            foreach (var to in tos)
            {
                Assert.Contains(to, allowedFrom);
            }
        }
    }

    [Fact]
    public void ProductionDagIsAcyclic()
    {
        var edges = CsprojGraph.ProductionEdges();
        var incoming = Eleven.ToDictionary(n => n, _ => 0);
        foreach (var (_, tos) in edges)
        {
            foreach (var to in tos)
            {
                if (incoming.TryGetValue(to, out var degree))
                {
                    incoming[to] = degree + 1;
                }
            }
        }

        var remaining = new HashSet<string>(Eleven);
        while (remaining.Count > 0)
        {
            var ready = remaining.Where(n => incoming[n] == 0).ToArray();
            Assert.True(ready.Length > 0, "cycle in production DAG");
            foreach (var node in ready)
            {
                remaining.Remove(node);
                if (!edges.TryGetValue(node, out var list))
                {
                    continue;
                }

                foreach (var to in list)
                {
                    if (incoming.TryGetValue(to, out var current))
                    {
                        incoming[to] = current - 1;
                    }
                }
            }
        }
    }
}
