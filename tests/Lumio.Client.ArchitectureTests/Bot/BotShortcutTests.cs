using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Bot;

public sealed class BotShortcutTests
{
    [Fact]
    public void BotPublicApiDoesNotReferenceLeafInternals()
    {
        var csproj = System.IO.Path.Combine(RepoRoot.Path, "modules", "bot", "src", "Lumio.Client.Bot.csproj");
        var xml = XDocument.Load(csproj);
        var refs = xml.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => MsBuildPath.ProjectName(e.Attribute("Include")!.Value))
            .ToArray();
        Assert.Contains("Lumio.Client.Session", refs);
        Assert.DoesNotContain("Lumio.Client.Connection", refs);
        Assert.DoesNotContain("Lumio.Client.Handshake", refs);
        Assert.DoesNotContain("Lumio.Client.Replica", refs);
        Assert.DoesNotContain("Lumio.Client.Prediction", refs);
    }
}
