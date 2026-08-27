using System.Reflection;
using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Architecture;

public sealed class ReplicaPublicApiTests
{
    private static readonly string[] BannedPrefixes =
    {
        "UnityEngine",
        "Serilog",
        "OpenTelemetry",
        "HybridCLR",
        "System.Net.Sockets",
        "GeneratedContract",
        "RuntimeContract"
    };

    private static readonly string[] BannedTypeNames =
    {
        "Envelope",
        "Transaction",
        "ErrorCode"
    };

    [Fact]
    public void NoCommitMethodAndNoPredictionReference()
    {
        var assembly = typeof(IClientReplica).Assembly;
        Assert.Equal("Lumio.Client.Replica", assembly.GetName().Name);

        MethodInfo? stage = typeof(IClientReplica).GetMethod(nameof(IClientReplica.StageAuthority));
        Assert.NotNull(stage);
        ParameterInfo[] stageParameters = stage!.GetParameters();
        Assert.Equal(3, stageParameters.Length);
        Assert.Equal(typeof(ReadOnlyMemory<byte>).MakeByRefType(), stageParameters[2].ParameterType);

        MethodInfo? observe = typeof(IClientReplica).GetMethod(nameof(IClientReplica.ObserveRuntimeOutcome));
        Assert.NotNull(observe);
        Assert.Contains(observe!.GetParameters(), parameter => parameter.Name == "outcome");

        foreach (var referenced in assembly.GetReferencedAssemblies())
        {
            Assert.NotEqual("Lumio.Client.Prediction", referenced.Name);
            Assert.NotEqual("Lumio.Client.Session", referenced.Name);
        }

        foreach (var type in assembly.GetExportedTypes())
        {
            Assert.DoesNotContain("Prediction", type.FullName, StringComparison.Ordinal);
            AssertBannedName(type);

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.False(string.Equals(method.Name, "Commit", StringComparison.OrdinalIgnoreCase), type.FullName + "." + method.Name);
                Assert.DoesNotContain("Prediction", method.Name, StringComparison.Ordinal);
                AssertPortType(method.ReturnType);
                foreach (var parameter in method.GetParameters())
                {
                    AssertPortType(parameter.ParameterType);
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                AssertPortType(property.PropertyType);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                AssertPortType(field.FieldType);
            }
        }
    }

    private static void AssertPortType(Type type)
    {
        Type inspect = Unwrap(type);
        string fullName = inspect.FullName ?? inspect.Name;
        foreach (var prefix in BannedPrefixes)
        {
            Assert.False(fullName.StartsWith(prefix, StringComparison.Ordinal), fullName);
        }

        AssertBannedName(inspect);
    }

    private static void AssertBannedName(Type type)
    {
        foreach (var bannedName in BannedTypeNames)
        {
            Assert.False(string.Equals(type.Name, bannedName, StringComparison.Ordinal), type.Name);
        }
    }

    private static Type Unwrap(Type type)
    {
        Type inspect = type.IsByRef || type.IsPointer || type.IsArray ? type.GetElementType() ?? type : type;
        if (inspect.IsGenericType)
        {
            foreach (var argument in inspect.GetGenericArguments())
            {
                AssertPortType(argument);
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        return inspect;
    }
}
