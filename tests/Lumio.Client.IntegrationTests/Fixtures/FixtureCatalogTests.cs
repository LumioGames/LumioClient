using System.Text.Json;

namespace Lumio.Client.IntegrationTests.Fixtures;

public sealed class FixtureCatalogTests
{
    [Fact]
    public void NoSchemaFieldsAreReplicatedLocally()
    {
        var catalog = GeneratedFixtureCatalog.LoadFromRepo();
        Assert.All(catalog.Root.LocalFaultScripts, script => Assert.Null(script.SchemaFields));
        using var doc = JsonDocument.Parse(File.ReadAllText(catalog.IndexPath));
        var json = doc.RootElement.GetRawText();
        Assert.DoesNotContain("fieldLayout", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"fields\":", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Envelope", json, StringComparison.Ordinal);
    }
}
