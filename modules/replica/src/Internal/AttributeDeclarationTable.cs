using System;
using System.Collections.Generic;

namespace Lumio.Client.Replica
{
    internal readonly struct AttributeDeclaration
    {
        public AttributeDeclaration(string attributeId, string valueType, string persistence, string replication, string visibility)
        {
            AttributeId = attributeId;
            ValueType = valueType;
            Persistence = persistence;
            Replication = replication;
            Visibility = visibility;
        }

        public string AttributeId { get; }

        public string ValueType { get; }

        public string Persistence { get; }

        public string Replication { get; }

        public string Visibility { get; }
    }

    internal static class AttributeDeclarationTable
    {
        private static readonly Dictionary<string, AttributeDeclaration> Table = Create();

        public static bool TryGet(string attributeId, out AttributeDeclaration declaration)
        {
            return Table.TryGetValue(attributeId, out declaration);
        }

        private static Dictionary<string, AttributeDeclaration> Create()
        {
            var table = new Dictionary<string, AttributeDeclaration>(StringComparer.Ordinal)
            {
                ["ChatComponent.lastMessageText"] = new AttributeDeclaration(
                    "ChatComponent.lastMessageText",
                    "utf8-string",
                    "persistent",
                    "not-replicated",
                    "server-only"),
                ["ChatComponent.lastMessageTick"] = new AttributeDeclaration(
                    "ChatComponent.lastMessageTick",
                    "u64",
                    "persistent",
                    "not-replicated",
                    "server-only"),
                ["ChatComponent.lastMessagePersistOnly"] = new AttributeDeclaration(
                    "ChatComponent.lastMessagePersistOnly",
                    "utf8-string",
                    "persistent",
                    "not-replicated",
                    "server-only"),
                ["EntityIdentity.entityType"] = new AttributeDeclaration(
                    "EntityIdentity.entityType",
                    "enum:entityType",
                    "ephemeral",
                    "replicated",
                    "room-public"),
                ["EntityIdentity.restrictedFlag"] = new AttributeDeclaration(
                    "EntityIdentity.restrictedFlag",
                    "utf8-string",
                    "ephemeral",
                    "replicated",
                    "claim-scoped")
            };
            return table;
        }
    }
}
