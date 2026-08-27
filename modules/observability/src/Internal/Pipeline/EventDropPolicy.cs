namespace Lumio.Client.Observability
{
    internal static class EventDropPolicy
    {
        public static bool IsEnqueueAllowed(EventSchemaClass schemaClass)
        {
            return schemaClass == EventSchemaClass.Critical
                || schemaClass == EventSchemaClass.Durable
                || schemaClass == EventSchemaClass.Droppable;
        }

        public static bool CanDropOnQueueFull(EventSchemaClass schemaClass)
        {
            return schemaClass == EventSchemaClass.Droppable;
        }
    }
}
