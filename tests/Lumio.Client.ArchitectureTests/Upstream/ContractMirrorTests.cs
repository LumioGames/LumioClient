using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lumio.Client.ArchitectureTests.Upstream;

/// <summary>
/// Guards the vendored architecture mirror. Every assertion here is existence
/// plus identity — never a count. Upstream grows additively by design, so a
/// count assertion would rot on exactly the changes the contract encourages.
/// </summary>
public sealed class ContractMirrorTests
{
    private const string MirrorDir = "contract-mirror/upstream";
    private const string LockFile = "contract-mirror/contract-mirror.sha256";
    private const string PinFile = "contract-mirror/MIRROR.md";

    [Fact]
    public void LockAndMirroredTreeCoverExactlyTheSameFiles()
    {
        var locked = ReadLock().Keys.ToHashSet(StringComparer.Ordinal);
        var present = EnumerateMirror().ToHashSet(StringComparer.Ordinal);

        var unlocked = present.Except(locked, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var vanished = locked.Except(present, StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        Assert.True(unlocked.Length == 0, "mirrored but not locked: " + string.Join(", ", unlocked));
        Assert.True(vanished.Length == 0, "locked but not mirrored: " + string.Join(", ", vanished));
        Assert.NotEmpty(locked);
    }

    [Fact]
    public void MirroredFilesStillHashToTheLockedValues()
    {
        var drifted = new List<string>();
        foreach (var (relative, expected) in ReadLock())
        {
            var full = System.IO.Path.Combine(RepoRoot.Path, relative);
            if (!File.Exists(full))
            {
                drifted.Add(relative + " (missing)");
                continue;
            }

            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                drifted.Add(relative + " (expected " + expected + ", got " + actual + ")");
            }
        }

        Assert.True(drifted.Count == 0, "mirror was hand-edited: " + string.Join("; ", drifted));
    }

    [Fact]
    public void PinRecordsAnUpstreamCommitAndTheBaselineEveryDescriptorAgreesWith()
    {
        var pinPath = System.IO.Path.Combine(RepoRoot.Path, PinFile);
        Assert.True(File.Exists(pinPath), PinFile + " must record the pin");
        var pin = File.ReadAllText(pinPath);

        var commit = Regex.Match(pin, "^- upstream commit: `([0-9a-f]{40})`", RegexOptions.Multiline);
        Assert.True(commit.Success, PinFile + " must name the upstream commit as a full 40-hex sha");

        var expectedBaseline = PinnedBaselineId();

        var descriptors = EnumerateMirror()
            .Where(p => p.EndsWith("artifact.descriptor.json", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(descriptors);

        foreach (var descriptor in descriptors)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(System.IO.Path.Combine(RepoRoot.Path, descriptor)));
            Assert.Equal(expectedBaseline, doc.RootElement.GetProperty("baselineId").GetString());
            Assert.Equal("LumioGameEngineArchitecture", doc.RootElement.GetProperty("publisher").GetString());
        }
    }

    [Fact]
    public void ArtifactRegistryAgreesWithTheMirroredPackagesItNames()
    {
        // packages/index.json is where an artifact's compilerHash, outputHash and
        // baselineId are authoritative. Assert that it agrees with the pin and that
        // every package it names is actually here — by name, never by count.
        var registryPath = System.IO.Path.Combine(RepoRoot.Path, MirrorDir, "packages/index.json");
        Assert.True(File.Exists(registryPath), "packages/index.json must be mirrored");

        using var doc = JsonDocument.Parse(File.ReadAllBytes(registryPath));
        var expectedBaseline = PinnedBaselineId();
        Assert.Equal(expectedBaseline, doc.RootElement.GetProperty("baselineId").GetString());

        var artifacts = doc.RootElement.GetProperty("artifacts").EnumerateArray().ToArray();
        Assert.NotEmpty(artifacts);

        foreach (var artifact in artifacts)
        {
            var id = artifact.GetProperty("artifactId").GetString();
            Assert.Equal(expectedBaseline, artifact.GetProperty("baselineId").GetString());
            Assert.Equal("LumioGameEngineArchitecture", artifact.GetProperty("publisher").GetString());
            Assert.Matches("^[0-9a-f]{64}$", artifact.GetProperty("compilerHash").GetString());

            var packagePath = artifact.GetProperty("packagePath").GetString()!.TrimEnd('/');
            var packageDir = System.IO.Path.Combine(RepoRoot.Path, MirrorDir, "packages", packagePath);
            Assert.True(Directory.Exists(packageDir), id + " names packagePath " + packagePath + ", which is not mirrored");

            var descriptor = System.IO.Path.Combine(packageDir, "artifact.descriptor.json");
            Assert.True(File.Exists(descriptor), id + " has no mirrored artifact.descriptor.json");
            using var side = JsonDocument.Parse(File.ReadAllBytes(descriptor));
            Assert.Equal(id, side.RootElement.GetProperty("artifactId").GetString());
            Assert.Equal(
                artifact.GetProperty("outputHash").GetString(),
                side.RootElement.GetProperty("outputHash").GetString());
        }
    }

    [Theory]
    [InlineData("schemas/replication-envelope.schema.json")]
    [InlineData("schemas/common.schema.json")]
    [InlineData("schemas/protocol-permission-gate.schema.json")]
    [InlineData("ids/index.json")]
    public void NamedContractSourcesAreOnRecordAndParse(string relative)
    {
        var full = System.IO.Path.Combine(RepoRoot.Path, MirrorDir, relative);
        Assert.True(File.Exists(full), relative + " must be mirrored — downstream cards read it as contract truth");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(full));
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void MirrorCarriesNoBuildableProjectOfItsOwn()
    {
        // A vendored .csproj under the repo root would inherit Directory.Build.props
        // (netstandard2.1 / LangVersion 9) and fight the upstream target framework.
        // Source is copied, never referenced as a project.
        var root = System.IO.Path.Combine(RepoRoot.Path, MirrorDir);
        Assert.True(Directory.Exists(root), MirrorDir + " must exist");
        var offenders = Directory.EnumerateFiles(root, "Directory.Build.*", SearchOption.AllDirectories)
            .Select(p => System.IO.Path.GetRelativePath(RepoRoot.Path, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        Assert.True(offenders.Length == 0, "mirror must not carry MSBuild directory files: " + string.Join(", ", offenders));
    }

    private static string PinnedBaselineId()
    {
        var pin = File.ReadAllText(System.IO.Path.Combine(RepoRoot.Path, PinFile));
        var match = Regex.Match(pin, "^- BaselineId: `([A-Za-z0-9.\\-]+)`", RegexOptions.Multiline);
        Assert.True(match.Success, PinFile + " must name the BaselineId");
        return match.Groups[1].Value;
    }

    private static Dictionary<string, string> ReadLock()
    {
        var lockPath = System.IO.Path.Combine(RepoRoot.Path, LockFile);
        Assert.True(File.Exists(lockPath), LockFile + " must exist");

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(lockPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var split = line.IndexOf("  ", StringComparison.Ordinal);
            Assert.True(split > 0, "malformed lock line: " + line);
            entries[line[(split + 2)..].Replace('\\', '/')] = line[..split];
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateMirror()
    {
        var root = System.IO.Path.Combine(RepoRoot.Path, MirrorDir);
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(p => System.IO.Path.GetRelativePath(RepoRoot.Path, p).Replace('\\', '/'));
    }
}
