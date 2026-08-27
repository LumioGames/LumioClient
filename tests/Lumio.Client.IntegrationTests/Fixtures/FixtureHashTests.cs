namespace Lumio.Client.IntegrationTests.Fixtures;

public sealed class FixtureHashTests
{
    [Fact]
    public void CatalogMatchesPinnedUpstreamHashes()
    {
        var catalog = GeneratedFixtureCatalog.LoadFromRepo();
        Assert.Equal(1, catalog.Root.Version);
        Assert.Equal("UPSTREAM-GENERATED-CONTRACT-API-MAP", catalog.Root.UpstreamCorpusPin.Requirement);
        if (catalog.Root.UpstreamCorpusPin.Status == "unpublished")
        {
            Assert.Empty(catalog.Root.UpstreamCorpusPin.Hashes);
            Assert.Null(catalog.Root.UpstreamCorpusPin.PackageId);
            return;
        }

        Assert.NotEmpty(catalog.Root.UpstreamCorpusPin.Hashes);
        Assert.All(catalog.Root.UpstreamCorpusPin.Hashes, hash => Assert.Equal(64, hash.Length));
    }
}
