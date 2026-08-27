using System.Reflection;

namespace Lumio.Client.ArchitectureTests;

internal static class RepoRoot
{
    public static string Path
    {
        get
        {
            var metadata = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "LumioClientRepoRoot")
                ?.Value;
            if (!string.IsNullOrWhiteSpace(metadata) && Directory.Exists(metadata))
            {
                return metadata;
            }

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(System.IO.Path.Combine(dir.FullName, "global.json"))
                    && Directory.Exists(System.IO.Path.Combine(dir.FullName, "modules")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("LumioClient repo root not found.");
        }
    }
}
