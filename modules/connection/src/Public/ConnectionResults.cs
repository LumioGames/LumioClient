namespace Lumio.Client.Connection
{
    public readonly struct ConnectionCommandResult
    {
        public ConnectionCommandResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct ConnectionSendResult
    {
        public ConnectionSendResult(bool accepted)
        {
            Accepted = accepted;
        }

        public bool Accepted { get; }
    }

    public readonly struct ClientConnectionSnapshot
    {
        public ClientConnectionSnapshot(ConnectionGeneration generation, bool terminal, int eventCount)
        {
            Generation = generation;
            Terminal = terminal;
            EventCount = eventCount;
        }

        public ConnectionGeneration Generation { get; }

        public bool Terminal { get; }

        public int EventCount { get; }
    }

    public readonly struct ClientConnectionCreateRequest
    {
        /// <summary>与本次改动之前的行为一致,不得随意改动。</summary>
        public const int DefaultEventCapacity = 32;

        /// <summary>与本次改动之前的行为一致,不得随意改动。</summary>
        public const int DefaultDrainLimit = 16;

        public ClientConnectionCreateRequest(ulong generation, int eventCapacity)
            : this(generation, eventCapacity, DefaultDrainLimit, default(ClientEndpoint))
        {
        }

        public ClientConnectionCreateRequest(ulong generation, int eventCapacity, int drainLimit, ClientEndpoint endpoint)
        {
            Generation = new ConnectionGeneration(generation);
            EventCapacity = eventCapacity;
            DrainLimit = drainLimit;
            Endpoint = endpoint;
        }

        public ConnectionGeneration Generation { get; }

        public int EventCapacity { get; }

        /// <summary>调用方每轮最多取走多少事件。</summary>
        public int DrainLimit { get; }

        /// <summary>远程目标与不透明认证材料;LocalEmbedded 路径为未配置。</summary>
        public ClientEndpoint Endpoint { get; }

        public override string ToString()
        {
            return string.Concat(
                "generation ",
                Generation.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ", capacity ",
                EventCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ", drain ",
                DrainLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ", endpoint ",
                Endpoint.ToString());
        }
    }

    public readonly struct ClientConnectionCreateResult
    {
        public ClientConnectionCreateResult(bool succeeded)
        {
            Succeeded = succeeded;
            Loopback = default!;
            HasLoopback = false;
        }

        public ClientConnectionCreateResult(bool succeeded, LocalEmbeddedLoopback loopback)
        {
            Succeeded = succeeded;
            Loopback = loopback;
            HasLoopback = loopback != null;
        }

        public bool Succeeded { get; }

        /// <summary>
        /// 只有 LocalEmbedded 工厂会给出环回端。远程工厂下这里为 null,
        /// 调用方必须先看 <see cref="HasLoopback"/> 或用 <see cref="TryGetLoopback"/>。
        /// </summary>
        public LocalEmbeddedLoopback Loopback { get; }

        public bool HasLoopback { get; }

        public bool TryGetLoopback([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out LocalEmbeddedLoopback loopback)
        {
            loopback = Loopback;
            return HasLoopback;
        }
    }
}
