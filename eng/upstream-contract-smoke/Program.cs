using System.Text.Json;
using System.Text.RegularExpressions;

var repo = FindRepo(AppContext.BaseDirectory);
var mapPath = Path.Combine(repo, "eng", "upstream-api-map.md");
var md = File.ReadAllText(mapPath);
var json = Regex.Match(md, "```json\\r?\\n([\\s\\S]*?)```").Groups[1].Value;
var doc = JsonDocument.Parse(json);

// Two blocked statuses, and the difference is the whole point of the mirror.
//   blocked-unpublished                     - nothing of the published surface was readable here.
//   blocked-absent-from-published-surface   - the surface is vendored and readable; the alias is not in it.
// Collapsing them would let a vendored mirror read as an unblocking event.
var blockedStatuses = new HashSet<string>(StringComparer.Ordinal)
{
    "blocked-unpublished",
    "blocked-absent-from-published-surface",
};

var blocked = 0;
foreach (var row in doc.RootElement.GetProperty("aliases").EnumerateArray())
{
    var alias = row.GetProperty("alias").GetString();
    var status = row.GetProperty("status").GetString();
    var published = row.GetProperty("publishedType");
    if (status is not null && blockedStatuses.Contains(status))
    {
        if (published.ValueKind != JsonValueKind.Null)
        {
            Console.Error.WriteLine("blocked alias must not invent a published type: " + alias);
            return 2;
        }

        blocked++;
        Console.WriteLine("BLOCKED " + alias + " " + row.GetProperty("blockId").GetString() + " " + status);
        continue;
    }

    if (status == "published")
    {
        if (published.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(published.GetString()))
        {
            Console.Error.WriteLine("published alias must name a type: " + alias);
            return 2;
        }

        Console.WriteLine("PUBLISHED " + alias + " -> " + published.GetString());
        continue;
    }

    Console.Error.WriteLine("unsupported status " + status);
    return 3;
}

// The map claims a mirror; the claim has to be checkable from inside this repo.
if (doc.RootElement.TryGetProperty("mirror", out var mirror))
{
    var lockFile = mirror.GetProperty("lockFile").GetString();
    var lockPath = Path.Combine(repo, lockFile!.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(lockPath))
    {
        Console.Error.WriteLine("map declares a mirror but its lock file is missing: " + lockFile);
        return 4;
    }

    var pinPath = Path.Combine(repo, "contract-mirror", "MIRROR.md");
    var pin = File.Exists(pinPath)
        ? Regex.Match(File.ReadAllText(pinPath), "^- upstream commit: `([0-9a-f]{40})`", RegexOptions.Multiline)
        : Match.Empty;
    if (!pin.Success)
    {
        Console.Error.WriteLine("map declares a mirror but contract-mirror/MIRROR.md records no pin");
        return 4;
    }

    var declared = mirror.GetProperty("sourceCommit").GetString();
    if (!string.Equals(declared, pin.Groups[1].Value, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("mirror pin disagrees: map says " + declared + ", MIRROR.md says " + pin.Groups[1].Value);
        return 4;
    }

    Console.WriteLine("mirror pinned at " + declared + " lock " + lockFile);
}

Console.WriteLine("compile-only smoke: aliases=" + doc.RootElement.GetProperty("aliases").GetArrayLength() + " blocked=" + blocked);
return 0;

static string FindRepo(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "global.json"))
            && File.Exists(Path.Combine(dir.FullName, "eng", "upstream-api-map.md")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("repo root not found");
}
