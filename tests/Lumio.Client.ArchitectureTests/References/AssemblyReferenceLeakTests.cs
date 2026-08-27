using System.Reflection;

namespace Lumio.Client.ArchitectureTests.References;

public sealed class AssemblyReferenceLeakTests
{
    private static readonly string[] Banned =
    {
        "UnityEngine",
        "UnityEngine.CoreModule",
        "Unity.InputSystem",
        "HybridCLR.Runtime",
        "System.Net.Sockets",
        "Serilog",
        "OpenTelemetry",
        "OpenTelemetry.Api"
    };

    [Fact]
    public void CoreHasNoUnityHybridClrSocketSupplierReferences()
    {
        var core = new[]
        {
            "Lumio.Client.Session",
            "Lumio.Client.Connection",
            "Lumio.Client.Handshake",
            "Lumio.Client.Replica",
            "Lumio.Client.Prediction",
            "Lumio.Client.Input",
            "Lumio.Client.Persistence",
            "Lumio.Client.Observability",
            "Lumio.Client.Bot"
        };

        foreach (var name in core)
        {
            var assembly = Assembly.Load(name);
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            foreach (var banned in Banned)
            {
                Assert.DoesNotContain(banned, referenced);
            }
        }
    }
}
