using System.Reflection;
using Lumio.Client.Prediction;

namespace Lumio.Client.Prediction.Tests.Architecture;

public sealed class PredictionPublicApiTests
{
    [Fact]
    public void NoCommitMethodAndNoReplicaReference()
    {
        Type port = typeof(IClientPrediction);
        string[] required =
        {
            nameof(IClientPrediction.AcceptCandidate),
            nameof(IClientPrediction.DiscardCandidateStage),
            nameof(IClientPrediction.ObserveLocalPredictionOutcome),
            nameof(IClientPrediction.StageAuthority),
            nameof(IClientPrediction.DiscardAuthorityStage),
            nameof(IClientPrediction.ObserveRuntimeOutcome),
            nameof(IClientPrediction.ResetForNewSession)
        };

        HashSet<string> declared = port
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string name in required)
        {
            Assert.Contains(name, declared);
        }

        Assembly assembly = port.Assembly;
        foreach (Type type in assembly.GetExportedTypes())
        {
            AssertNoBannedName(type.Name);
            Assert.DoesNotContain("Replica", type.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GeneratedContract", type.FullName, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeContract", type.FullName, StringComparison.Ordinal);

            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.DoesNotContain("Commit", method.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Replica", method.Name, StringComparison.OrdinalIgnoreCase);
                AssertNoBannedName(method.Name);
                Inspect(method.ReturnType);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Inspect(parameter.ParameterType);
                }
            }
        }
    }

    private static void Inspect(Type type)
    {
        Type inspect = type.IsByRef ? type.GetElementType() ?? type : type;
        if (inspect.IsGenericType)
        {
            foreach (Type argument in inspect.GetGenericArguments())
            {
                Inspect(argument);
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        string fullName = inspect.FullName ?? inspect.Name;
        Assert.DoesNotContain("Replica", fullName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Envelope", fullName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transaction", fullName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ErrorCode", fullName, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoBannedName(string name)
    {
        Assert.DoesNotContain("Envelope", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transaction", name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ErrorCode", name, StringComparison.OrdinalIgnoreCase);
    }
}
