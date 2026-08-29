using System.Xml.Linq;

namespace Lumio.Client.ArchitectureTests;

internal static class Allowlist
{
    public static IReadOnlyDictionary<string, string[]> Load()
    {
        var json = File.ReadAllText(System.IO.Path.Combine(RepoRoot.Path, "eng", "project-reference-allowlist.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var row in doc.RootElement.GetProperty("production").EnumerateObject())
        {
            map[row.Name] = row.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
        }

        return map;
    }
}

internal static class MsBuildPath
{
    // ProjectReference Include 一律用反斜杠分隔。反斜杠在非 Windows 上不是目录分隔符,
    // 直接交给 GetFileNameWithoutExtension 会原样返回整串而不报错——静默失败。
    public static string ProjectName(string include)
    {
        return System.IO.Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
    }
}

internal static class CsprojGraph
{
    public static Dictionary<string, string[]> ProductionEdges()
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var csproj in Directory.EnumerateFiles(System.IO.Path.Combine(RepoRoot.Path, "modules"), "*.csproj", SearchOption.AllDirectories))
        {
            if (csproj.Contains($"{System.IO.Path.DirectorySeparatorChar}tests{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || csproj.Contains($"{System.IO.Path.DirectorySeparatorChar}host{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var xml = XDocument.Load(csproj);
            var name = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value
                       ?? System.IO.Path.GetFileNameWithoutExtension(csproj);
            var refs = xml.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => MsBuildPath.ProjectName(e.Attribute("Include")!.Value))
                .ToArray();
            result[name] = refs;
        }

        return result;
    }
}
