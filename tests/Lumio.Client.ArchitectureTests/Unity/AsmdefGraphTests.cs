using System.Text.Json;

namespace Lumio.Client.ArchitectureTests.Unity;

public sealed class AsmdefGraphTests
{
    [Fact]
    public void UnityReferencesAreSubsetOfProjectAllowlist()
    {
        var allow = Allowlist.Load();
        foreach (var (name, refs, _) in LoadAsmdefs())
        {
            Assert.True(allow.TryGetValue(name, out var allowed), name);
            foreach (var r in refs)
            {
                Assert.Contains(r, allowed);
            }
        }
    }

    [Fact]
    public void CoreAsmdefsDoNotReferenceUnityEngine()
    {
        foreach (var (name, refs, noEngine) in LoadAsmdefs())
        {
            Assert.DoesNotContain("UnityEngine", refs);
            Assert.DoesNotContain("UnityEditor", refs);
            if (name is "Lumio.Client.UnityAdapter" or "Lumio.Client.HybridClrAdapter")
            {
                continue;
            }

            Assert.True(noEngine, name + " must set noEngineReferences");
        }
    }

    [Fact]
    public void UnityAndHybridClrStayAtLeaves()
    {
        foreach (var (name, refs, _) in LoadAsmdefs())
        {
            if (name is "Lumio.Client.UnityAdapter" or "Lumio.Client.HybridClrAdapter")
            {
                continue;
            }

            Assert.DoesNotContain("Lumio.Client.UnityAdapter", refs);
            Assert.DoesNotContain("Lumio.Client.HybridClrAdapter", refs);
        }
    }

    private static IEnumerable<(string Name, string[] Refs, bool NoEngine)> LoadAsmdefs()
    {
        var root = System.IO.Path.Combine(RepoRoot.Path, "packages", "com.lumio.client");
        Assert.True(File.Exists(System.IO.Path.Combine(root, "package.json")));
        foreach (var file in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var name = doc.RootElement.GetProperty("name").GetString()!;
            var refs = doc.RootElement.GetProperty("references").EnumerateArray().Select(e => e.GetString()!).ToArray();
            var noEngine = doc.RootElement.GetProperty("noEngineReferences").GetBoolean();
            yield return (name, refs, noEngine);
        }
    }
}
