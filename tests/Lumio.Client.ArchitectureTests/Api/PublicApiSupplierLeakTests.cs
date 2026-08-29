using System.Reflection;

namespace Lumio.Client.ArchitectureTests.Api;

public sealed class PublicApiSupplierLeakTests
{
    private static readonly string[] StablePortAssemblies =
    {
        "Lumio.Client.Session",
        "Lumio.Client.Connection",
        "Lumio.Client.Handshake",
        "Lumio.Client.Replica",
        "Lumio.Client.Prediction",
        "Lumio.Client.Input",
        "Lumio.Client.Persistence",
        "Lumio.Client.Observability",
        "Lumio.Client.Bot",
        "Lumio.Client.UnityAdapter",
        "Lumio.Client.HybridClrAdapter"
    };

    [Fact]
    public void NoThirdPartyTypeCrossesStablePorts()
    {
        var leaks = new List<string>();

        foreach (var name in StablePortAssemblies)
        {
            foreach (var type in Assembly.Load(name).GetExportedTypes())
            {
                leaks.AddRange(SupplierLeakScanner.Scan(type));
            }
        }

        Assert.True(leaks.Count == 0, string.Join(Environment.NewLine, leaks));
    }
}
