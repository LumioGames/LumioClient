using System.Reflection;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.Unit;

public sealed class ClientEventWriterTests
{
    private static readonly string[] BannedPrefixes =
    {
        "UnityEngine",
        "Serilog",
        "OpenTelemetry",
        "HybridCLR",
        "System.Net.Sockets"
    };

    private static readonly string[] BannedTypeNames =
    {
        "Envelope",
        "Transaction",
        "ErrorCode",
        "Socket"
    };

    [Fact]
    public static void PublicPortUsesOnlyGeneratedAndModuleTypes()
    {
        var assembly = typeof(IClientEventWriter).Assembly;
        Assert.Equal("Lumio.Client.Observability", assembly.GetName().Name);

        foreach (var type in assembly.GetExportedTypes())
        {
            AssertPortType(type);

            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    AssertPortType(parameter.ParameterType);
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
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

    [Fact]
    public static void InvalidSchemaClassIsRejectedWithoutQueueMutation()
    {
        var factory = new ClientEventPipelineFactory();
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        var created = factory.Create(in options, new NoopClientEventSink(), out var writer);
        Assert.True(created.Succeeded);
        Assert.NotNull(writer);

        var before = writer.GetSnapshot();
        var record = new ClientEventRecord(EventSchemaClass.Invalid, ReadOnlyMemory<byte>.Empty, null);
        var result = writer.TryWrite(in record);
        var after = writer.GetSnapshot();

        Assert.False(result.Succeeded);
        Assert.Equal(before.QueueDepth, after.QueueDepth);
    }

    private static void AssertPortType(Type type)
    {
        var inspect = Unwrap(type);
        var fullName = inspect.FullName ?? inspect.Name;
        foreach (var prefix in BannedPrefixes)
        {
            Assert.False(fullName.StartsWith(prefix, StringComparison.Ordinal), fullName);
        }

        foreach (var bannedName in BannedTypeNames)
        {
            Assert.False(string.Equals(inspect.Name, bannedName, StringComparison.Ordinal), inspect.Name);
        }
    }

    private static Type Unwrap(Type type)
    {
        var inspect = type.IsByRef || type.IsArray ? type.GetElementType() ?? type : type;
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

    private sealed class NoopClientEventSink : IClientEventSink
    {
        public ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return new ValueTask<ClientEventSinkResult>(new ClientEventSinkResult(true, records.Length, false));
        }
    }
}
