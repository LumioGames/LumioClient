using System.Diagnostics.CodeAnalysis;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.EventDrops;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Foundation named tests.")]
public sealed class EventDropPolicyProperties
{
    public static TheoryData<EventSchemaClass> SchemaClassTable
    {
        get
        {
            return new TheoryData<EventSchemaClass>
            {
                EventSchemaClass.Invalid,
                EventSchemaClass.Critical,
                EventSchemaClass.Durable,
                EventSchemaClass.Droppable,
                (EventSchemaClass)4,
                (EventSchemaClass)32,
                (EventSchemaClass)255
            };
        }
    }

    [Theory]
    [MemberData(nameof(SchemaClassTable))]
    public static void NeverDropsDurableClass(EventSchemaClass schemaClass)
    {
        var canDrop = EventDropPolicy.CanDropOnQueueFull(schemaClass);
        Assert.False(EventDropPolicy.CanDropOnQueueFull(EventSchemaClass.Durable));
        Assert.False(EventDropPolicy.CanDropOnQueueFull(EventSchemaClass.Critical));
        if (schemaClass == EventSchemaClass.Droppable)
        {
            Assert.True(canDrop);
            return;
        }

        Assert.False(canDrop);
    }
}
