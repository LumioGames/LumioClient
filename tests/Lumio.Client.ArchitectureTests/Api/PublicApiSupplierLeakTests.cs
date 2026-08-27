using System.Reflection;

namespace Lumio.Client.ArchitectureTests.Api;

public sealed class PublicApiSupplierLeakTests
{
    private static readonly string[] BannedPrefixes =
    {
        "UnityEngine",
        "Unity.InputSystem",
        "HybridCLR",
        "Serilog",
        "OpenTelemetry",
        "System.Net.Sockets"
    };

    [Fact]
    public void NoThirdPartyTypeCrossesStablePorts()
    {
        var names = new[]
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

        foreach (var name in names)
        {
            var assembly = Assembly.Load(name);
            foreach (var type in assembly.GetExportedTypes())
            {
                AssertNoBanned(type);
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    AssertNoBanned(method.ReturnType);
                    foreach (var parameter in method.GetParameters())
                    {
                        AssertNoBanned(parameter.ParameterType);
                    }
                }
            }
        }
    }

    private static void AssertNoBanned(Type type)
    {
        var inspect = type.IsByRef ? type.GetElementType() ?? type : type;
        if (inspect.IsGenericType)
        {
            foreach (var arg in inspect.GetGenericArguments())
            {
                AssertNoBanned(arg);
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        var fullName = inspect.FullName ?? inspect.Name;
        foreach (var prefix in BannedPrefixes)
        {
            Assert.False(fullName.StartsWith(prefix, StringComparison.Ordinal), fullName);
        }
    }
}
