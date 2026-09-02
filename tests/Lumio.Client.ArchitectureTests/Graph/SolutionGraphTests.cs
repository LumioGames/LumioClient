using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests.Graph;

public sealed class SolutionGraphTests
{
    [Fact]
    public void SlnxTestGraphDoesNotUnconditionallyRequireMissingOrOutOfRepoProjects()
    {
        string repo = RepoRoot.Path;
        string slnx = Path.Combine(repo, "LumioClient.slnx");
        Assert.True(File.Exists(slnx), slnx);

        var missing = new List<string>();
        foreach (string csproj in SlnxProjects.Enumerate(slnx, repo))
        {
            var xml = XDocument.Load(csproj);
            foreach (XElement reference in xml.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                if (SlnxProjects.IsExistsGated(reference))
                {
                    continue;
                }

                string include = reference.Attribute("Include")?.Value ?? string.Empty;
                if (include.Length == 0 || include.Contains("$("))
                {
                    missing.Add(csproj + " -> " + include + " (ungated property include)");
                    continue;
                }

                string resolved = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(csproj)!,
                    include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
                bool insideRepo = resolved.StartsWith(
                    repo.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                    || string.Equals(resolved, repo, StringComparison.OrdinalIgnoreCase);
                if (!insideRepo)
                {
                    missing.Add(csproj + " -> " + include + " (outside repo: " + resolved + ")");
                    continue;
                }

                if (!File.Exists(resolved))
                {
                    missing.Add(csproj + " -> " + include + " (missing: " + resolved + ")");
                }
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }
}

internal static class SlnxProjects
{
    public static IEnumerable<string> Enumerate(string slnx, string repo)
    {
        var xml = XDocument.Load(slnx);
        foreach (XElement project in xml.Descendants().Where(e => e.Name.LocalName == "Project"))
        {
            string? relative = project.Attribute("Path")?.Value;
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            yield return Path.GetFullPath(Path.Combine(
                repo,
                relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    public static bool IsExistsGated(XElement element)
    {
        for (XElement? current = element; current is not null; current = current.Parent)
        {
            string? condition = current.Attribute("Condition")?.Value;
            if (!string.IsNullOrEmpty(condition)
                && condition.Contains("Exists(", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
