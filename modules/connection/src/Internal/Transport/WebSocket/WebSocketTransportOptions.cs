using System;
using System.Globalization;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// 远程 WS 传输的资源预算。
    /// </summary>
    /// <remarks>
    /// 本类型与 <see cref="WebSocketClientConnectionFactory"/> 是 public,却落在
    /// <c>Internal/Transport/WebSocket/</c> 目录下——这是 ADR 0003 裁决三加 T-00003 边界的直接后果:
    /// 落点被限定在该目录,而 CC-2 要求交付一个可被组装根消费的 <see cref="IClientConnectionFactory"/> 实现。
    /// 目录名在此只表达「远程传输适配器的实现区」,不表达可见性。搬迁到 <c>Public/</c> 需另立卡。
    /// </remarks>
    public readonly struct WebSocketTransportOptions
    {
        /// <summary>与 LumioServer <c>TransportProvisionalDefaults.DefaultMaxMessageBytes</c> 对齐(provisional)。</summary>
        public const int DefaultMaxMessageBytes = 65536;

        /// <summary>R-00258 §3.2 登记的 <c>transportPolicy.maxMessageBytes</c> 上限,不得超过。</summary>
        public const int MaxAllowedMessageBytes = 1048576;

        /// <summary>
        /// <c>maxFragmentBytes</c> **只作声明值**:本层不实现任何分片重组,
        /// 该常量不参与接收路径的任何判断(架构源 <c>ABS-WIRE-FRAGMENTATION</c>)。
        /// </summary>
        public const int DeclaredMaxFragmentBytes = 65536;

        /// <summary>固定接收缓冲:入站永远按它分配,绝不按对端声称的长度分配。</summary>
        public const int DefaultReceiveBufferBytes = 8192;

        /// <summary>与 LumioServer <c>TransportProvisionalDefaults.IdleTimeoutSeconds</c> 对齐(provisional)。</summary>
        public const int DefaultIdleTimeoutSeconds = 15;

        public WebSocketTransportOptions(int maxMessageBytes, int receiveBufferBytes, TimeSpan idleTimeout)
        {
            MaxMessageBytes = maxMessageBytes;
            ReceiveBufferBytes = receiveBufferBytes;
            IdleTimeout = idleTimeout;
        }

        public static WebSocketTransportOptions Default
        {
            get
            {
                return new WebSocketTransportOptions(
                    DefaultMaxMessageBytes,
                    DefaultReceiveBufferBytes,
                    TimeSpan.FromSeconds(DefaultIdleTimeoutSeconds));
            }
        }

        public int MaxMessageBytes { get; }

        public int ReceiveBufferBytes { get; }

        public TimeSpan IdleTimeout { get; }

        public bool TryValidate(out string reason)
        {
            if (MaxMessageBytes <= 0)
            {
                reason = "maxMessageBytes must be positive";
                return false;
            }

            if (MaxMessageBytes > MaxAllowedMessageBytes)
            {
                reason = string.Concat(
                    "maxMessageBytes must not exceed ",
                    MaxAllowedMessageBytes.ToString(CultureInfo.InvariantCulture),
                    " (R-00258 §3.2)");
                return false;
            }

            if (ReceiveBufferBytes <= 0)
            {
                reason = "receiveBufferBytes must be positive";
                return false;
            }

            if (IdleTimeout <= TimeSpan.Zero)
            {
                reason = "idleTimeout must be positive";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
