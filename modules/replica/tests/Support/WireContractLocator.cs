namespace Lumio.Client.Replica.Tests.Support;

/// <summary>
/// Locates C-1 / C-2 living wire JSON on architecture origin/main (SHA 2b7e321).
/// Does not embed a second protocol copy. Skip when the architecture checkout is absent.
/// </summary>
internal static class WireContractLocator
{
    public const string GameplayEnvelopeFileName = "gameplay-command-envelope-v1.json";
    public const string EntityBindingFileName = "entity-binding-and-query-v1.json";
    public const string ArchitectureRootVariable = "LUMIO_ARCHITECTURE_ROOT";
    public const string GameplayEnvelopeVariable = "LUMIO_GAMEPLAY_ENVELOPE_CONTRACT";
    public const string EntityBindingVariable = "LUMIO_ENTITY_BINDING_CONTRACT";

    public static string? LocateGameplayEnvelope()
    {
        return Locate(GameplayEnvelopeVariable, GameplayEnvelopeFileName);
    }

    public static string? LocateEntityBinding()
    {
        return Locate(EntityBindingVariable, EntityBindingFileName);
    }

    private static string? Locate(string fileVariable, string fileName)
    {
        string? fromFile = Environment.GetEnvironmentVariable(fileVariable);
        if (!string.IsNullOrEmpty(fromFile) && File.Exists(fromFile))
        {
            return fromFile;
        }

        string? fromRoot = Environment.GetEnvironmentVariable(ArchitectureRootVariable);
        if (!string.IsNullOrEmpty(fromRoot))
        {
            string rooted = Path.Combine(fromRoot, "engine", "wire", fileName);
            if (File.Exists(rooted))
            {
                return rooted;
            }
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string[] candidates =
            {
                Path.Combine(directory.FullName, "LumioGameEngineArchitecture", "engine", "wire", fileName),
                Path.Combine(directory.FullName, "wt-arch", "merge-wave0", "engine", "wire", fileName),
                Path.Combine(directory.FullName, "wt-arch", "merge-main", "engine", "wire", fileName),
                Path.Combine(directory.FullName, "engine", "wire", fileName)
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }
}
