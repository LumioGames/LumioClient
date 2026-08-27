namespace Lumio.Client.ArchitectureTests.References;

public sealed class ProjectReferenceAllowlistTests
{
    [Fact]
    public void ActualEdgesEqualAllowedSubset()
    {
        var allow = Allowlist.Load();
        var actual = CsprojGraph.ProductionEdges();
        foreach (var (from, tos) in actual)
        {
            Assert.True(allow.TryGetValue(from, out var allowedFrom), from);
            foreach (var to in tos)
            {
                Assert.Contains(to, allowedFrom);
            }
        }

        foreach (var (from, allowed) in allow)
        {
            Assert.True(actual.TryGetValue(from, out var actualFrom), from);
            foreach (var to in actualFrom)
            {
                Assert.Contains(to, allowed);
            }
        }
    }
}
