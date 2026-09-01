using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lumio.Client.Hello;

public sealed record HelloLimits(
    long MaxPayloadBytes,
    long MaxSessions,
    long IngressQueuePerSession,
    long HelloLogCapacity,
    long HandshakeTimeoutMs,
    long BaselineTimeoutMs,
    long ScenarioTimeoutMs);

public sealed record ContractFieldError(string Field, string Problem);

/// <summary>
/// hello-wire-v1 契约的只读视图。字段清单、limits 与 botTrace 事件词表全部从契约文件解析,
/// 本类不复制任何协议真值——契约文件(架构仓 engine/wire/hello-wire-v1.json)是唯一来源。
/// required 值的约束记法(const:/enum:/u64/epoch-ms/sha256-hex/bool/string/array:)在
/// <see cref="ValidateField"/> 里按固定文法解释,文法本身是契约格式的一部分。
/// </summary>
public sealed class HelloContract : IDisposable
{
    private static readonly Regex Sha256HexPattern = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private readonly JsonDocument _document;
    private readonly Dictionary<string, Dictionary<string, string>> _requiredByMessage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _requiredBySharedType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _botTraceRequired = new(StringComparer.Ordinal);

    private HelloContract(string path, JsonDocument document)
    {
        Path = path;
        _document = document;
        JsonElement root = document.RootElement;

        ContractId = ReadString(root, "contractId")
            ?? throw new InvalidDataException("契约缺少 contractId");
        JsonElement transport = root.TryGetProperty("transport", out JsonElement transportElement)
            ? transportElement
            : throw new InvalidDataException("契约缺少 transport");
        Subprotocol = ReadString(transport, "subprotocol")
            ?? throw new InvalidDataException("契约缺少 transport.subprotocol");
        MaxFrameBytes = root.TryGetProperty("transport", out transportElement)
            && transportElement.TryGetProperty("maxFrameBytes", out JsonElement maxFrame)
            && maxFrame.TryGetInt64(out long frameBytes)
            ? frameBytes
            : 65536L;

        Roles = ReadStringArray(root, "roles");
        ErrorCodes = ReadStringArray(root, "errorCodes");

        foreach (JsonProperty message in root.GetProperty("messages").EnumerateObject())
        {
            _requiredByMessage[message.Name] = ReadFieldMap(message.Value.GetProperty("required"));
        }

        foreach (JsonProperty shared in root.GetProperty("sharedTypes").EnumerateObject())
        {
            _requiredBySharedType[shared.Name] = ReadFieldMap(shared.Value.GetProperty("required"));
        }

        foreach (JsonProperty kind in root.GetProperty("process").GetProperty("botTraceEventKinds").EnumerateObject())
        {
            _botTraceRequired[kind.Name] = ReadStringArray(kind.Value, "required");
        }

        JsonElement limits = root.GetProperty("limits");
        Limits = new HelloLimits(
            ReadInt64(limits, "maxPayloadBytes"),
            ReadInt64(limits, "maxSessions"),
            ReadInt64(limits, "ingressQueuePerSession"),
            ReadInt64(limits, "helloLogCapacity"),
            ReadInt64(limits, "handshakeTimeoutMs"),
            ReadInt64(limits, "baselineTimeoutMs"),
            ReadInt64(limits, "scenarioTimeoutMs"));
    }

    public static HelloContract Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("契约路径为空", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("契约文件不存在", path);
        }

        JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        try
        {
            return new HelloContract(System.IO.Path.GetFullPath(path), document);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public string Path { get; }

    public string ContractId { get; }

    public string Subprotocol { get; }

    public long MaxFrameBytes { get; }

    public IReadOnlyList<string> Roles { get; }

    public IReadOnlyList<string> ErrorCodes { get; }

    public HelloLimits Limits { get; }

    public IReadOnlyList<string> MessageTypes
    {
        get
        {
            lock (_requiredByMessage)
            {
                return new List<string>(_requiredByMessage.Keys);
            }
        }
    }

    public IReadOnlyList<string> BotTraceKinds
    {
        get
        {
            lock (_botTraceRequired)
            {
                return new List<string>(_botTraceRequired.Keys);
            }
        }
    }

    public bool IsKnownMessageType(string messageType)
    {
        lock (_requiredByMessage)
        {
            return _requiredByMessage.ContainsKey(messageType);
        }
    }

    public IReadOnlyDictionary<string, string> RequiredFields(string messageType)
    {
        lock (_requiredByMessage)
        {
            return _requiredByMessage.TryGetValue(messageType, out Dictionary<string, string>? fields)
                ? new Dictionary<string, string>(fields, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public IReadOnlyList<string> BotTraceRequiredFields(string kind)
    {
        lock (_botTraceRequired)
        {
            return _botTraceRequired.TryGetValue(kind, out string[]? fields)
                ? new List<string>(fields)
                : new List<string>();
        }
    }

    /// <summary>契约 required 值里的 const 记法(如 kind=const:hello)由本方法读出,调用方不硬编码。</summary>
    public string? ConstValue(string messageType, string field)
    {
        IReadOnlyDictionary<string, string> fields = RequiredFields(messageType);
        return fields.TryGetValue(field, out string? constraint)
            && constraint.StartsWith("const:", StringComparison.Ordinal)
            ? constraint["const:".Length..]
            : null;
    }

    public IReadOnlyList<ContractFieldError> ValidateMessage(JsonElement message)
    {
        var errors = new List<ContractFieldError>();
        if (message.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ContractFieldError("$", "not an object"));
            return errors;
        }

        if (!message.TryGetProperty("messageType", out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ContractFieldError("messageType", "missing or not a string"));
            return errors;
        }

        string messageType = typeElement.GetString()!;
        Dictionary<string, string> required;
        lock (_requiredByMessage)
        {
            if (!_requiredByMessage.TryGetValue(messageType, out required!))
            {
                errors.Add(new ContractFieldError("messageType", "unknown_mapping: " + messageType));
                return errors;
            }
        }

        foreach (KeyValuePair<string, string> field in required)
        {
            ValidateField(errors, message, field.Key, field.Value, field.Key);
        }

        return errors;
    }

    public void Dispose()
    {
        _document.Dispose();
    }

    /// <param name="field">JSON 属性名(查找键)。</param>
    /// <param name="displayName">错误报告用的字段名;数组元素递归时带 <c>x[].y</c> 前缀,不作查找键。</param>
    private void ValidateField(List<ContractFieldError> errors, JsonElement parent, string field, string constraint, string displayName)
    {
        if (!parent.TryGetProperty(field, out JsonElement value))
        {
            errors.Add(new ContractFieldError(displayName, "missing required field"));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ContractFieldError(displayName, "null"));
            return;
        }

        if (constraint.StartsWith("const:", StringComparison.Ordinal))
        {
            string expected = constraint["const:".Length..];
            if (value.ValueKind != JsonValueKind.String || value.GetString() != expected)
            {
                errors.Add(new ContractFieldError(displayName, "must equal const " + expected));
            }
        }
        else if (constraint == "enum:roles")
        {
            if (value.ValueKind != JsonValueKind.String || !IsRole(value.GetString()!))
            {
                errors.Add(new ContractFieldError(displayName, "must be one of roles(" + string.Join("|", Roles) + ")"));
            }
        }
        else if (constraint == "u64" || constraint == "epoch-ms")
        {
            if (value.ValueKind != JsonValueKind.Number
                || !value.TryGetInt64(out long number)
                || number < 0)
            {
                errors.Add(new ContractFieldError(displayName, "must be a non-negative integer(" + constraint + ")"));
            }
        }
        else if (constraint == "sha256-hex")
        {
            string? hex = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (hex is null || !Sha256HexPattern.IsMatch(hex))
            {
                errors.Add(new ContractFieldError(displayName, "must be 64 lowercase hex characters"));
            }
        }
        else if (constraint == "bool")
        {
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add(new ContractFieldError(displayName, "must be a boolean"));
            }
        }
        else if (constraint == "string")
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                errors.Add(new ContractFieldError(displayName, "must be a string"));
            }
        }
        else if (constraint.StartsWith("array:", StringComparison.Ordinal))
        {
            string shared = constraint["array:".Length..];
            if (value.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new ContractFieldError(displayName, "must be an array of " + shared));
                return;
            }

            Dictionary<string, string>? elementRequired;
            lock (_requiredBySharedType)
            {
                _requiredBySharedType.TryGetValue(shared, out elementRequired);
            }

            if (elementRequired is null)
            {
                return;
            }

            foreach (JsonElement element in value.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new ContractFieldError(displayName, "element must be a " + shared + " object"));
                    continue;
                }

                foreach (KeyValuePair<string, string> elementField in elementRequired)
                {
                    ValidateField(errors, element, elementField.Key, elementField.Value, displayName + "[]." + elementField.Key);
                }
            }
        }

        // 未知约束记法只做存在性检查(契约 additive 演进不应让旧客户端误拒)。
    }

    private static Dictionary<string, string> ReadFieldMap(JsonElement element)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            map[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return map;
    }

    private bool IsRole(string candidate)
    {
        foreach (string role in Roles)
        {
            if (string.Equals(role, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string[] ReadStringArray(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (JsonElement element in value.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                values.Add(element.GetString()!);
            }
        }

        return values.ToArray();
    }

    private static long ReadInt64(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out long number)
            ? number
            : 0L;
    }
}
