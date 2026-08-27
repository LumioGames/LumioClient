using System.IO;
using System.Reflection;
using Lumio.Client.Persistence;

namespace Lumio.Client.Persistence.Tests.Unit;

public sealed class PersistencePublicContractTests
{
    [Fact]
    public static void NoPathStreamOrDatabaseTypeCrossesPort()
    {
        _ = typeof(IVerifiedSessionArtifactSource);
        _ = typeof(IClientCheckpointStore);
        _ = typeof(IClientPersistenceFactory);
        _ = typeof(VerifiedArtifactReadRequest);
        _ = typeof(VerifiedArtifactReadResult);
        _ = typeof(CheckpointReadRequest);
        _ = typeof(CheckpointReadResult);
        _ = typeof(CheckpointWriteRequest);
        _ = typeof(CheckpointWriteResult);
        _ = typeof(PersistenceSnapshot);

        var assembly = typeof(IVerifiedSessionArtifactSource).Assembly;
        foreach (var type in assembly.GetExportedTypes())
        {
            InspectType(type);

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.False(
                    property.Name == "Path" && property.PropertyType == typeof(string),
                    type.FullName + " exposes public string Path.");
                InspectType(property.PropertyType);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                InspectType(field.FieldType);
            }

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    InspectType(parameter.ParameterType);
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                InspectType(method.ReturnType);
                foreach (var parameter in method.GetParameters())
                {
                    InspectType(parameter.ParameterType);
                }
            }
        }
    }

    [Fact]
    public static async Task OnlyVerifiedArtifactCanBeReturned()
    {
        var factory = IClientPersistenceFactory.CreateMemory();
        var source = Assert.IsType<MemoryVerifiedSessionArtifactSource>(factory.CreateVerifiedSessionArtifactSource());
        var generation = 7UL;
        var payload = new byte[] { 1, 2, 3 };
        var contentHash = new byte[] { 9, 9 };
        var request = new VerifiedArtifactReadRequest("config", "rel-1", contentHash, generation);

        source.SeedUnverified(in request, payload);
        var unverified = await source.ReadAsync(in request, CancellationToken.None);

        Assert.False(unverified.Succeeded);
        Assert.False(unverified.Verified);
        Assert.True(unverified.Payload.IsEmpty);
        Assert.Equal(generation, unverified.Generation);

        source.SeedVerified(in request, payload);
        var verified = await source.ReadAsync(in request, CancellationToken.None);

        Assert.True(verified.Succeeded);
        Assert.True(verified.Verified);
        Assert.True(verified.Payload.Span.SequenceEqual(payload));
        Assert.Equal(generation, verified.Generation);
    }

    private static void InspectType(Type type)
    {
        if (type == typeof(void) || type.IsGenericParameter)
        {
            return;
        }

        var inspect = type;
        while (inspect.IsByRef || inspect.IsPointer || inspect.IsArray)
        {
            inspect = inspect.GetElementType() ?? inspect;
        }

        if (inspect.IsGenericType)
        {
            foreach (var argument in inspect.GetGenericArguments())
            {
                InspectType(argument);
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        var fullName = inspect.FullName ?? inspect.Name;
        Assert.False(IsStreamLike(inspect), fullName);
        Assert.False(IsDatabaseLike(inspect), fullName);
    }

    private static bool IsStreamLike(Type type)
    {
        return typeof(Stream).IsAssignableFrom(type)
            || string.Equals(type.Name, nameof(Stream), StringComparison.Ordinal)
            || string.Equals(type.Name, nameof(FileStream), StringComparison.Ordinal);
    }

    private static bool IsDatabaseLike(Type type)
    {
        var name = type.Name;
        var fullName = type.FullName ?? name;
        return ContainsIgnoreCase(name, "DbConnection")
            || ContainsIgnoreCase(fullName, "DbConnection")
            || ContainsIgnoreCase(name, "Sqlite")
            || ContainsIgnoreCase(fullName, "Sqlite");
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
