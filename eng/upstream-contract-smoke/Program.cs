using System.Text.Json;
using System.Text.RegularExpressions;

var repo = FindRepo(AppContext.BaseDirectory);
var mapPath = Path.Combine(repo, "eng", "upstream-api-map.md");
var md = File.ReadAllText(mapPath);
var json = Regex.Match(md, "```json\\r?\\n([\\s\\S]*?)```").Groups[1].Value;
var doc = JsonDocument.Parse(json);
var blocked = 0;
foreach (var row in doc.RootElement.GetProperty("aliases").EnumerateArray())
{
    var alias = row.GetProperty("alias").GetString();
    var status = row.GetProperty("status").GetString();
    var published = row.GetProperty("publishedType");
    if (status == "blocked-unpublished")
    {
        if (published.ValueKind != JsonValueKind.Null)
        {
            Console.Error.WriteLine("blocked alias must not invent a published type: " + alias);
            return 2;
        }

        blocked++;
        Console.WriteLine("BLOCKED " + alias + " " + row.GetProperty("blockId").GetString());
        continue;
    }

    Console.Error.WriteLine("unsupported status " + status);
    return 3;
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
