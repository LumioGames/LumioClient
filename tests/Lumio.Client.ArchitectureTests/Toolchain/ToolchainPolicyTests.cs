using System.Text.Json;
using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Toolchain;

public sealed class ToolchainPolicyTests
{
    [Fact]
    public void GlobalJsonPinsSdkAndDisablesRollForward()
    {
        var path = System.IO.Path.Combine(RepoRoot.Path, "global.json");
        Assert.True(File.Exists(path), "global.json must exist");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var sdk = doc.RootElement.GetProperty("sdk");
        Assert.Equal("10.0.400", sdk.GetProperty("version").GetString());
        Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [Fact]
    public void AllProjectsEnableNullableAndWarningsAsErrors()
    {
        var propsPath = System.IO.Path.Combine(RepoRoot.Path, "Directory.Build.props");
        Assert.True(File.Exists(propsPath), "Directory.Build.props must exist");
        var props = XDocument.Load(propsPath);
        Assert.Equal("enable", Property(props, "Nullable"));
        Assert.Equal("true", Property(props, "TreatWarningsAsErrors"));

        foreach (var csproj in Directory.EnumerateFiles(RepoRoot.Path, "*.csproj", SearchOption.AllDirectories))
        {
            var xml = XDocument.Load(csproj);
            var nullable = Property(xml, "Nullable");
            var warnings = Property(xml, "TreatWarningsAsErrors");
            if (nullable is not null)
            {
                Assert.Equal("enable", nullable);
            }

            if (warnings is not null)
            {
                Assert.Equal("true", warnings);
            }
        }
    }

    private static string? Property(XDocument document, string name)
    {
        return document.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();
    }
}
