using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Hello;

/// <summary>
/// 独立 Headless Bot 的 CLI 入口。返回进程退出码:0 成功、1 失败、3 参数错误。
/// Host 只做 <c>Main => RunAsync(args)</c>;全部流程可在测试进程内被直接驱动。
/// 流程:连接 → handshake → baseline/BaselineAck → **等待 sender=browser 的 Delta(发送前提)**
/// → 发 InputCommand(sequence=1、payload="Hello World") → 等 server 正常关闭。
/// </summary>
public static class HelloBotCli
{
    public const int ExitOk = 0;
    public const int ExitFailure = 1;
    public const int ExitUsage = 3;

    private const string ScenarioPayload = "Hello World";

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (!HelloBotArguments.TryParse(args, out HelloBotArguments? parsed, out string? usageError))
        {
            await stderr.WriteLineAsync(HelloBotArguments.Usage).ConfigureAwait(false);
            await stderr.WriteLineAsync("error: " + usageError).ConfigureAwait(false);
            return ExitUsage;
        }

        HelloContract contract;
        try
        {
            contract = HelloContract.Load(parsed!.ContractPath);
        }
        catch (Exception ex)
        {
            await WriteResultAsync(parsed!.ResultPath, failure: new ResultFailure(
                parsed.Role,
                null,
                "contract_load_failed: " + ex.Message,
                Array.Empty<WireError>())).ConfigureAwait(false);
            await stderr.WriteLineAsync("contract load failed: " + ex.Message).ConfigureAwait(false);
            return ExitFailure;
        }

        using (contract)
        using (var trace = new BotTrace(parsed.TracePath, contract))
        {
            trace.Write("bot_started", new Dictionary<string, object?>
            {
                ["pid"] = (long)Environment.ProcessId,
                ["role"] = parsed.Role,
                ["serverUrl"] = parsed.Url,
            });

            if (!IsRole(contract, parsed.Role))
            {
                await WriteResultAsync(parsed.ResultPath, failure: new ResultFailure(
                    parsed.Role,
                    null,
                    "unknown_role: " + parsed.Role,
                    Array.Empty<WireError>())).ConfigureAwait(false);
                trace.Write("bot_finished", new Dictionary<string, object?>
                {
                    ["exitOk"] = false,
                    ["receivedBySender"] = new Dictionary<string, object?>(),
                });
                return ExitFailure;
            }

            long baselineTimeoutMs = parsed.BaselineTimeoutMs ?? contract.Limits.BaselineTimeoutMs;
            long scenarioTimeoutMs = contract.Limits.ScenarioTimeoutMs;
            var options = new HelloWireClientOptions
            {
                ServerUrl = parsed.Url,
                Role = parsed.Role,
                ClientName = parsed.ClientName,
                Contract = contract,
            };

            HelloWireClient? client = null;
            bool ok = false;
            string reason = string.Empty;
            try
            {
                client = new HelloWireClient(options);
                client.Event += helloEvent => trace.Write(helloEvent.Kind, helloEvent.Fields);

                await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                await client.HandshakeAsync(cancellationToken).ConfigureAwait(false);

                using (var baselineTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    baselineTimeout.CancelAfter(TimeSpan.FromMilliseconds(baselineTimeoutMs));
                    try
                    {
                        await client.WaitForBaselineAsync(baselineTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (baselineTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new HelloWireException("baseline_timeout", $"FullSnapshot 未在 {baselineTimeoutMs}ms 内到达");
                    }
                }

                using (var scenarioTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    scenarioTimeout.CancelAfter(TimeSpan.FromMilliseconds(scenarioTimeoutMs));
                    try
                    {
                        // 发送前提:先收到 browser 的 Delta。
                        await client.WaitForDeltaAsync("browser", scenarioTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (scenarioTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new HelloWireException("scenario_timeout", $"未在 {scenarioTimeoutMs}ms 内收到 browser Delta");
                    }
                }

                if (parsed.CommandDelayMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(parsed.CommandDelayMs), cancellationToken).ConfigureAwait(false);
                }

                await client.SendCommandAsync(ScenarioPayload, cancellationToken).ConfigureAwait(false);
                trace.Write("command_result", new Dictionary<string, object?>
                {
                    ["ok"] = true,
                    ["detail"] = "command delivered",
                });

                using (var closeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    closeTimeout.CancelAfter(TimeSpan.FromMilliseconds(scenarioTimeoutMs));
                    try
                    {
                        await client.WaitForServerCloseAsync(closeTimeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (closeTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new HelloWireException("scenario_timeout", "server 未在 scenarioTimeoutMs 内关闭连接");
                    }
                }

                ok = true;
            }
            catch (HelloWireException ex)
            {
                reason = ex.Code + ": " + ex.Message;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                reason = "cancelled";
            }
            catch (Exception ex)
            {
                reason = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                if (client is not null)
                {
                    await client.AbortAsync("run finished").ConfigureAwait(false);
                }
            }

            await WriteResultAsync(
                parsed.ResultPath,
                success: ok && client is not null ? ToSuccess(parsed.Role, client) : null,
                failure: !ok ? new ResultFailure(parsed.Role, client?.SessionId, reason, client?.Errors ?? Array.Empty<WireError>()) : null).ConfigureAwait(false);

            var receivedBySender = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (client is not null)
            {
                foreach (DeltaRecord delta in client.ReceivedDeltas)
                {
                    receivedBySender.TryGetValue(delta.Sender, out object? current);
                    receivedBySender[delta.Sender] = (current as long? ?? 0L) + 1L;
                }
            }

            trace.Write("bot_finished", new Dictionary<string, object?>
            {
                ["exitOk"] = ok,
                ["receivedBySender"] = receivedBySender,
            });

            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }

            if (!ok)
            {
                await stderr.WriteLineAsync("bot failed: " + reason).ConfigureAwait(false);
                return ExitFailure;
            }

            await stdout.WriteLineAsync($"hello bot ok: session={client?.SessionId} latencyMsMax={client?.MaxLatencyMs}").ConfigureAwait(false);
            return ExitOk;
        }
    }

    private static bool IsRole(HelloContract contract, string role)
    {
        foreach (string candidate in contract.Roles)
        {
            if (string.Equals(candidate, role, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ResultSuccess ToSuccess(string role, HelloWireClient client)
    {
        CommandRecord? sent = client.LastSentCommand;
        return new ResultSuccess(
            role,
            client.SessionId ?? string.Empty,
            client.ReceivedDeltas,
            sent?.Sequence ?? 0L,
            sent?.PayloadSha256 ?? string.Empty,
            sent?.SentAtMs ?? 0L,
            Math.Max(client.MaxLatencyMs, 0L));
    }

    private static async Task WriteResultAsync(string path, ResultSuccess? success = null, ResultFailure? failure = null)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        if (success is not null)
        {
            writer.WriteBoolean("ok", true);
            writer.WriteString("role", success.Role);
            writer.WriteString("sessionId", success.SessionId);
            writer.WriteStartArray("received");
            foreach (DeltaRecord delta in success.Received)
            {
                writer.WriteStartObject();
                writer.WriteString("sender", delta.Sender);
                writer.WriteNumber("sequence", delta.Sequence);
                writer.WriteNumber("tickId", delta.TickId);
                writer.WriteNumber("revision", delta.Revision);
                writer.WriteString("payloadSha256", delta.PayloadSha256);
                writer.WriteNumber("latencyMs", delta.LatencyMs);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartObject("sent");
            writer.WriteNumber("sequence", success.SentSequence);
            writer.WriteString("payloadSha256", success.SentPayloadSha256);
            writer.WriteNumber("sentAtMs", success.SentAtMs);
            writer.WriteEndObject();
            writer.WriteNumber("maxLatencyMs", success.MaxLatencyMs);
        }
        else if (failure is not null)
        {
            writer.WriteBoolean("ok", false);
            writer.WriteString("role", failure.Role);
            if (failure.SessionId is not null)
            {
                writer.WriteString("sessionId", failure.SessionId);
            }

            writer.WriteString("reason", failure.Reason);
            writer.WriteStartArray("errors");
            foreach (WireError error in failure.Errors)
            {
                writer.WriteStartObject();
                writer.WriteString("code", error.Code);
                writer.WriteString("detail", error.Detail);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private sealed record ResultSuccess(
        string Role,
        string SessionId,
        IReadOnlyList<DeltaRecord> Received,
        long SentSequence,
        string SentPayloadSha256,
        long SentAtMs,
        long MaxLatencyMs);

    private sealed record ResultFailure(
        string Role,
        string? SessionId,
        string Reason,
        IReadOnlyList<WireError> Errors);
}

internal sealed record HelloBotArguments
{
    public const string Usage =
        "usage: Lumio.Client.HelloBot --url ws://host:port/path --role bot --contract <hello-wire-v1.json>"
        + " --trace <bot.ndjson> --result <result.json>"
        + " [--client-name lumio-bot] [--baseline-timeout-ms 5000] [--command-delay-ms 0]";

    public required string Url { get; init; }

    public required string Role { get; init; }

    public required string ContractPath { get; init; }

    public required string TracePath { get; init; }

    public required string ResultPath { get; init; }

    public string ClientName { get; init; } = "lumio-bot";

    public long? BaselineTimeoutMs { get; init; }

    public long CommandDelayMs { get; init; }

    public static bool TryParse(string[] args, out HelloBotArguments? parsed, out string? error)
    {
        parsed = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            if (!flag.StartsWith("--", StringComparison.Ordinal))
            {
                error = "unexpected argument: " + flag;
                return false;
            }

            if (i + 1 >= args.Length)
            {
                error = "missing value for " + flag;
                return false;
            }

            if (values.ContainsKey(flag))
            {
                error = "duplicate flag: " + flag;
                return false;
            }

            values[flag] = args[++i];
        }

        foreach (string required in new[] { "--url", "--role", "--contract", "--trace", "--result" })
        {
            if (!values.TryGetValue(required, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                error = "missing required flag: " + required;
                return false;
            }
        }

        foreach (string flag in values.Keys)
        {
            if (flag is not ("--url" or "--role" or "--contract" or "--trace" or "--result"
                or "--client-name" or "--baseline-timeout-ms" or "--command-delay-ms"))
            {
                error = "unknown flag: " + flag;
                return false;
            }
        }

        string url = values["--url"];
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            error = "--url must be an absolute ws:// or wss:// URI: " + url;
            return false;
        }

        long? baselineTimeoutMs = null;
        if (values.TryGetValue("--baseline-timeout-ms", out string? baselineRaw))
        {
            if (!long.TryParse(baselineRaw, NumberStyles.None, CultureInfo.InvariantCulture, out long baseline)
                || baseline <= 0)
            {
                error = "--baseline-timeout-ms must be a positive integer: " + baselineRaw;
                return false;
            }

            baselineTimeoutMs = baseline;
        }

        long commandDelayMs = 0;
        if (values.TryGetValue("--command-delay-ms", out string? delayRaw))
        {
            if (!long.TryParse(delayRaw, NumberStyles.None, CultureInfo.InvariantCulture, out long delay)
                || delay < 0)
            {
                error = "--command-delay-ms must be a non-negative integer: " + delayRaw;
                return false;
            }

            commandDelayMs = delay;
        }

        parsed = new HelloBotArguments
        {
            Url = url,
            Role = values["--role"],
            ContractPath = values["--contract"],
            TracePath = values["--trace"],
            ResultPath = values["--result"],
            ClientName = values.TryGetValue("--client-name", out string? clientName) ? clientName : "lumio-bot",
            BaselineTimeoutMs = baselineTimeoutMs,
            CommandDelayMs = commandDelayMs,
        };
        error = null;
        return true;
    }
}
