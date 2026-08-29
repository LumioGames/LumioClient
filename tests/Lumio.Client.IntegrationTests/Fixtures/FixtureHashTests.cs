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

        Assert.Equal("mirrored", catalog.Root.UpstreamCorpusPin.Status);
        Assert.Matches("^[0-9a-f]{40}$", catalog.Root.UpstreamCorpusPin.SourceCommit);
        Assert.NotEmpty(catalog.Root.UpstreamCorpusPin.Hashes);

        // Recompute rather than only checking the shape: a pin nobody verifies is a
        // comment. Every recorded path must exist and still hash to the recorded value.
        foreach (var pinned in catalog.Root.UpstreamCorpusPin.Hashes)
        {
            Assert.Equal(64, pinned.Sha256.Length);
            var full = System.IO.Path.Combine(RepoRoot.Path, pinned.Path);
            Assert.True(File.Exists(full), pinned.Path + " is pinned but missing");
            var actual = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
            Assert.Equal(actual, pinned.Sha256);
        }

        var corpus = System.IO.Path.Combine(RepoRoot.Path, catalog.Root.UpstreamCorpusPin.CorpusRoot!);
        Assert.True(Directory.Exists(corpus), "mirrored fixture corpus must exist");
    }
}
