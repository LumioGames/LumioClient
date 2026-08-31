using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Lumio.Client.Hello;

public interface IHelloTrace : IDisposable
{
    void Write(string kind, IEnumerable<KeyValuePair<string, object?>> fields);
}

/// <summary>
/// NDJSON 追加写 tracer。事件种类与必填字段按契约 process.botTraceEventKinds 校验:
/// 未知 kind 或缺必填字段即抛出(编程错误),不静默落盘残缺审计行。
/// 每行 shape 为 {kind, receivedAtMs, ...fields},逐行 flush。
/// </summary>
public sealed class BotTrace : IHelloTrace
{
    private readonly HelloContract _contract;
    private readonly object _gate = new();
    private FileStream? _stream;
    private bool _disposed;

    public BotTrace(string path, HelloContract contract)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("trace 路径为空", nameof(path));
        }

        Path = path;
        _contract = contract;
    }

    public string Path { get; }

    public void Write(string kind, IEnumerable<KeyValuePair<string, object?>> fields)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(fields);

        IReadOnlyList<string> required = _contract.BotTraceRequiredFields(kind);
        if (required.Count == 0 && !ContainsKind(kind))
        {
            throw new InvalidOperationException("unknown bot trace kind: " + kind);
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> field in fields)
        {
            values[field.Key] = field.Value;
        }

        foreach (string field in required)
        {
            if (!values.ContainsKey(field))
            {
                throw new InvalidOperationException($"bot trace {kind} 缺契约必填字段 {field}");
            }
        }

        using var line = new MemoryStream();
        using (var writer = new Utf8JsonWriter(line))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind);
            writer.WriteNumber("receivedAtMs", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            foreach (KeyValuePair<string, object?> field in values)
            {
                WriteValue(writer, field.Key, field.Value);
            }

            writer.WriteEndObject();
        }

        byte[] newline = Encoding.UTF8.GetBytes("\n");
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _stream ??= File.Open(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _stream.Write(line.ToArray());
            _stream.Write(newline);
            _stream.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _stream?.Dispose();
        }
    }

    private bool ContainsKind(string kind)
    {
        foreach (string candidate in _contract.BotTraceKinds)
        {
            if (string.Equals(candidate, kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(key);
                break;
            case string s:
                writer.WriteString(key, s);
                break;
            case bool b:
                writer.WriteBoolean(key, b);
                break;
            case long l:
                writer.WriteNumber(key, l);
                break;
            case int i:
                writer.WriteNumber(key, i);
                break;
            case IDictionary<string, object?> nested:
                writer.WriteStartObject(key);
                foreach (KeyValuePair<string, object?> field in nested)
                {
                    WriteValue(writer, field.Key, field.Value);
                }

                writer.WriteEndObject();
                break;
            default:
                throw new InvalidOperationException("unsupported trace value type " + value.GetType().Name);
        }
    }
}
