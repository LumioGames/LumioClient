namespace Lumio.Client.ArchitectureTests.Layout;

public sealed class ForbiddenModuleTests
{
    [Fact]
    public void NoCommonSharedUtilsOrSecondContractsModule()
    {
        var modulesRoot = System.IO.Path.Combine(RepoRoot.Path, "modules");
        var forbidden = new[]
        {
            "common", "shared", "utils", "contracts", "composition", "presentation", "config", "auth", "replay"
        };
        foreach (var name in forbidden)
        {
            Assert.False(Directory.Exists(System.IO.Path.Combine(modulesRoot, name)), name);
        }

        var actual = Directory.GetDirectories(modulesRoot).Select(System.IO.Path.GetFileName).OrderBy(n => n).ToArray();
        var expected = new[]
        {
            "bot", "connection", "handshake", "hybridclr-adapter", "input", "observability",
            "persistence", "prediction", "replica", "session", "unity-adapter"
        };
        Assert.Equal(expected, actual);
    }
}
