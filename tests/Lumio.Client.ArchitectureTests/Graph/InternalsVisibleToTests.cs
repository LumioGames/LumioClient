using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Graph;

public sealed class InternalsVisibleToTests
{
    [Fact]
    public void OnlyOwnTestAssemblyIsFriend()
    {
        foreach (var csproj in Directory.EnumerateFiles(System.IO.Path.Combine(RepoRoot.Path, "modules"), "*.csproj", SearchOption.AllDirectories))
        {
            if (!csproj.Contains($"{System.IO.Path.DirectorySeparatorChar}src{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var xml = XDocument.Load(csproj);
            var assembly = xml.Descendants().First(e => e.Name.LocalName == "AssemblyName").Value;
            var friends = xml.Descendants()
                .Where(e => e.Name.LocalName == "InternalsVisibleTo")
                .Select(e => e.Attribute("Include")!.Value)
                .ToArray();
            Assert.Equal(new[] { assembly + ".Tests" }, friends);
        }
    }
}
