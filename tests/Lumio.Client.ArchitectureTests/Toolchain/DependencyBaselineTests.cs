using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Toolchain;

public sealed class DependencyBaselineTests
{
    [Fact]
    public void AllDirectPackagesHaveLicenseAndLockStrategy()
    {
        var packagesPath = System.IO.Path.Combine(RepoRoot.Path, "Directory.Packages.props");
        var baselinePath = System.IO.Path.Combine(RepoRoot.Path, "eng", "dependency-baseline.md");
        Assert.True(File.Exists(packagesPath));
        Assert.True(File.Exists(baselinePath));
        var baseline = File.ReadAllText(baselinePath);
        Assert.Contains("packages.lock.json", baseline, StringComparison.Ordinal);

        var xml = XDocument.Load(packagesPath);
        var versions = xml.Descendants()
            .Where(e => e.Name.LocalName == "PackageVersion")
            .Select(e => (
                Id: e.Attribute("Include")?.Value,
                Version: e.Attribute("Version")?.Value))
            .ToArray();
        Assert.NotEmpty(versions);

        foreach (var (id, version) in versions)
        {
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.False(string.IsNullOrWhiteSpace(version));
            Assert.Contains($"| {id} | {version} |", baseline, StringComparison.Ordinal);
            var row = baseline.Split('\n').First(line => line.StartsWith($"| {id} |", StringComparison.Ordinal));
            Assert.Matches(new Regex("MIT|Apache-2.0|BSD-3-Clause", RegexOptions.IgnoreCase), row);
            Assert.Contains("lock", row, StringComparison.OrdinalIgnoreCase);
        }
    }
}
