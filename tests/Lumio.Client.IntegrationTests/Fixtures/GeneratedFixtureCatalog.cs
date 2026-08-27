using System.Text.Json;

namespace Lumio.Client.IntegrationTests.Fixtures;

public sealed class GeneratedFixtureCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeneratedFixtureCatalog(string indexPath)
    {
        IndexPath = indexPath;
        using var doc = JsonDocument.Parse(File.ReadAllText(indexPath));
        Root = JsonSerializer.Deserialize<CatalogRoot>(doc.RootElement.GetRawText(), SerializerOptions)
               ?? throw new InvalidOperationException("fixture catalog missing");
    }

    public string IndexPath { get; }

    public CatalogRoot Root { get; }

    public static GeneratedFixtureCatalog LoadFromRepo()
    {
        return new GeneratedFixtureCatalog(System.IO.Path.Combine(RepoRoot.Path, "tests", "Fixtures", "index.json"));
    }
}

public sealed class CatalogRoot
{
    public int Version { get; set; }

    public UpstreamCorpusPin UpstreamCorpusPin { get; set; } = new();

    public LocalFaultScript[] LocalFaultScripts { get; set; } = Array.Empty<LocalFaultScript>();
}

public sealed class UpstreamCorpusPin
{
    public string Requirement { get; set; } = "";

    public string Status { get; set; } = "";

    public string? PackageId { get; set; }

    public string? PackageVersion { get; set; }

    public string[] Hashes { get; set; } = Array.Empty<string>();
}

public sealed class LocalFaultScript
{
    public string Id { get; set; } = "";

    public string Description { get; set; } = "";

    public object? SchemaFields { get; set; }
}
