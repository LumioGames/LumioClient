using System;
using System.Collections.Generic;
using System.Text;

namespace Lumio.Client.Replica
{
    internal enum LiteKind
    {
        Null = 0,
        Bool = 1,
        Number = 2,
        String = 3,
        Array = 4,
        Object = 5
    }

    internal sealed class LiteNode
    {
        private LiteNode(LiteKind kind, bool boolValue, ulong numberValue, string stringValue, IReadOnlyList<LiteNode> items, IReadOnlyDictionary<string, LiteNode> fields)
        {
            Kind = kind;
            BoolValue = boolValue;
            NumberValue = numberValue;
            StringValue = stringValue;
            Items = items;
            Fields = fields;
        }

        public LiteKind Kind { get; }

        public bool BoolValue { get; }

        public ulong NumberValue { get; }

        public string StringValue { get; }

        public IReadOnlyList<LiteNode> Items { get; }

        public IReadOnlyDictionary<string, LiteNode> Fields { get; }

        public static LiteNode Null()
        {
            return new LiteNode(LiteKind.Null, false, 0UL, string.Empty, Array.Empty<LiteNode>(), EmptyFields);
        }

        public static LiteNode FromBool(bool value)
        {
            return new LiteNode(LiteKind.Bool, value, 0UL, string.Empty, Array.Empty<LiteNode>(), EmptyFields);
        }

        public static LiteNode FromNumber(ulong value)
        {
            return new LiteNode(LiteKind.Number, false, value, string.Empty, Array.Empty<LiteNode>(), EmptyFields);
        }

        public static LiteNode FromString(string value)
        {
            return new LiteNode(LiteKind.String, false, 0UL, value, Array.Empty<LiteNode>(), EmptyFields);
        }

        public static LiteNode FromArray(List<LiteNode> items)
        {
            return new LiteNode(LiteKind.Array, false, 0UL, string.Empty, items.ToArray(), EmptyFields);
        }

        public static LiteNode FromObject(Dictionary<string, LiteNode> fields)
        {
            return new LiteNode(LiteKind.Object, false, 0UL, string.Empty, Array.Empty<LiteNode>(), fields);
        }

        public bool TryGet(string name, out LiteNode node)
        {
            return Fields.TryGetValue(name, out node);
        }

        public bool TryGetString(string name, out string value)
        {
            value = string.Empty;
            if (!TryGet(name, out LiteNode node) || node.Kind != LiteKind.String)
            {
                return false;
            }

            value = node.StringValue;
            return true;
        }

        public bool TryGetUInt64(string name, out ulong value)
        {
            value = 0UL;
            if (!TryGet(name, out LiteNode node) || node.Kind != LiteKind.Number)
            {
                return false;
            }

            value = node.NumberValue;
            return true;
        }

        public bool TryGetArray(string name, out IReadOnlyList<LiteNode> items)
        {
            items = Array.Empty<LiteNode>();
            if (!TryGet(name, out LiteNode node) || node.Kind != LiteKind.Array)
            {
                return false;
            }

            items = node.Items;
            return true;
        }

        private static readonly IReadOnlyDictionary<string, LiteNode> EmptyFields =
            new Dictionary<string, LiteNode>(StringComparer.Ordinal);
    }

    internal static class LiteJsonParser
    {
        private const int MaxDepth = 8;

        public static bool LooksLikeObject(ReadOnlySpan<byte> utf8)
        {
            int i = 0;
            SkipWs(utf8, ref i);
            return i < utf8.Length && utf8[i] == (byte)'{';
        }

        public static bool TryParse(ReadOnlySpan<byte> utf8, out LiteNode node)
        {
            byte[] copy = utf8.ToArray();
            int index = 0;
            if (!TryParseValue(copy, ref index, 0, out node))
            {
                return false;
            }

            SkipWs(copy, ref index);
            return index == copy.Length;
        }

        private static bool TryParseValue(byte[] src, ref int index, int depth, out LiteNode node)
        {
            node = LiteNode.Null();
            if (depth > MaxDepth)
            {
                return false;
            }

            SkipWs(src, ref index);
            if (index >= src.Length)
            {
                return false;
            }

            byte b = src[index];
            if (b == (byte)'{')
            {
                return TryParseObject(src, ref index, depth, out node);
            }

            if (b == (byte)'[')
            {
                return TryParseArray(src, ref index, depth, out node);
            }

            if (b == (byte)'"')
            {
                if (!TryParseString(src, ref index, out string text))
                {
                    return false;
                }

                node = LiteNode.FromString(text);
                return true;
            }

            if (b == (byte)'t')
            {
                if (!TryConsume(src, ref index, "true"))
                {
                    return false;
                }

                node = LiteNode.FromBool(true);
                return true;
            }

            if (b == (byte)'f')
            {
                if (!TryConsume(src, ref index, "false"))
                {
                    return false;
                }

                node = LiteNode.FromBool(false);
                return true;
            }

            if (b == (byte)'n')
            {
                if (!TryConsume(src, ref index, "null"))
                {
                    return false;
                }

                node = LiteNode.Null();
                return true;
            }

            return TryParseNumber(src, ref index, out node);
        }

        private static bool TryParseObject(byte[] src, ref int index, int depth, out LiteNode node)
        {
            node = LiteNode.Null();
            index++;
            var fields = new Dictionary<string, LiteNode>(StringComparer.Ordinal);
            SkipWs(src, ref index);
            if (index < src.Length && src[index] == (byte)'}')
            {
                index++;
                node = LiteNode.FromObject(fields);
                return true;
            }

            while (index < src.Length)
            {
                SkipWs(src, ref index);
                if (!TryParseString(src, ref index, out string key))
                {
                    return false;
                }

                SkipWs(src, ref index);
                if (index >= src.Length || src[index] != (byte)':')
                {
                    return false;
                }

                index++;
                if (!TryParseValue(src, ref index, depth + 1, out LiteNode value))
                {
                    return false;
                }

                fields[key] = value;
                SkipWs(src, ref index);
                if (index >= src.Length)
                {
                    return false;
                }

                if (src[index] == (byte)'}')
                {
                    index++;
                    node = LiteNode.FromObject(fields);
                    return true;
                }

                if (src[index] != (byte)',')
                {
                    return false;
                }

                index++;
            }

            return false;
        }

        private static bool TryParseArray(byte[] src, ref int index, int depth, out LiteNode node)
        {
            node = LiteNode.Null();
            index++;
            var items = new List<LiteNode>();
            SkipWs(src, ref index);
            if (index < src.Length && src[index] == (byte)']')
            {
                index++;
                node = LiteNode.FromArray(items);
                return true;
            }

            while (index < src.Length)
            {
                if (!TryParseValue(src, ref index, depth + 1, out LiteNode item))
                {
                    return false;
                }

                items.Add(item);
                SkipWs(src, ref index);
                if (index >= src.Length)
                {
                    return false;
                }

                if (src[index] == (byte)']')
                {
                    index++;
                    node = LiteNode.FromArray(items);
                    return true;
                }

                if (src[index] != (byte)',')
                {
                    return false;
                }

                index++;
            }

            return false;
        }

        private static bool TryParseString(byte[] src, ref int index, out string value)
        {
            value = string.Empty;
            if (index >= src.Length || src[index] != (byte)'"')
            {
                return false;
            }

            index++;
            var builder = new StringBuilder();
            while (index < src.Length)
            {
                byte b = src[index++];
                if (b == (byte)'"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (b == (byte)'\\')
                {
                    if (index >= src.Length)
                    {
                        return false;
                    }

                    byte esc = src[index++];
                    switch (esc)
                    {
                        case (byte)'"':
                        case (byte)'\\':
                        case (byte)'/':
                            builder.Append((char)esc);
                            break;
                        case (byte)'b':
                            builder.Append('\b');
                            break;
                        case (byte)'f':
                            builder.Append('\f');
                            break;
                        case (byte)'n':
                            builder.Append('\n');
                            break;
                        case (byte)'r':
                            builder.Append('\r');
                            break;
                        case (byte)'t':
                            builder.Append('\t');
                            break;
                        case (byte)'u':
                            if (index + 4 > src.Length)
                            {
                                return false;
                            }

                            int code = 0;
                            for (int n = 0; n < 4; n++)
                            {
                                int nib = Nibble(src[index++]);
                                if (nib < 0)
                                {
                                    return false;
                                }

                                code = (code << 4) | nib;
                            }

                            builder.Append((char)code);
                            break;
                        default:
                            return false;
                    }

                    continue;
                }

                if (b < 0x20)
                {
                    return false;
                }

                builder.Append((char)b);
            }

            return false;
        }

        private static bool TryParseNumber(byte[] src, ref int index, out LiteNode node)
        {
            node = LiteNode.Null();
            int start = index;
            if (index < src.Length && src[index] == (byte)'-')
            {
                return false;
            }

            if (index >= src.Length || src[index] < (byte)'0' || src[index] > (byte)'9')
            {
                return false;
            }

            if (src[index] == (byte)'0')
            {
                index++;
                if (index < src.Length && src[index] >= (byte)'0' && src[index] <= (byte)'9')
                {
                    return false;
                }
            }
            else
            {
                while (index < src.Length && src[index] >= (byte)'0' && src[index] <= (byte)'9')
                {
                    index++;
                }
            }

            if (index < src.Length && (src[index] == (byte)'.' || src[index] == (byte)'e' || src[index] == (byte)'E'))
            {
                return false;
            }

            ulong value = 0UL;
            for (int i = start; i < index; i++)
            {
                ulong digit = (ulong)(src[i] - (byte)'0');
                if (value > (ulong.MaxValue - digit) / 10UL)
                {
                    return false;
                }

                value = (value * 10UL) + digit;
            }

            node = LiteNode.FromNumber(value);
            return true;
        }

        private static bool TryConsume(byte[] src, ref int index, string token)
        {
            if (index + token.Length > src.Length)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                if (src[index + i] != (byte)token[i])
                {
                    return false;
                }
            }

            index += token.Length;
            return true;
        }

        private static void SkipWs(byte[] src, ref int index)
        {
            SkipWs(src.AsSpan(), ref index);
        }

        private static void SkipWs(ReadOnlySpan<byte> src, ref int index)
        {
            while (index < src.Length)
            {
                byte b = src[index];
                if (b != (byte)' ' && b != (byte)'\n' && b != (byte)'\r' && b != (byte)'\t')
                {
                    return;
                }

                index++;
            }
        }

        private static int Nibble(byte b)
        {
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                return b - (byte)'0';
            }

            if (b >= (byte)'a' && b <= (byte)'f')
            {
                return b - (byte)'a' + 10;
            }

            if (b >= (byte)'A' && b <= (byte)'F')
            {
                return b - (byte)'A' + 10;
            }

            return -1;
        }
    }
}
