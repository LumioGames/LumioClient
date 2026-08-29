using System;
using System.Globalization;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// 一次连接尝试的目标与通道认证材料。凭据与 nonce 在本层是不透明字节:
    /// 本仓不定义其格式、算法、轮换或派生规则,只负责原样携带并保证不外泄。
    /// 每个连接代次都必须带自己的 nonce(架构源 D-012:V1 无 Resume Token)。
    /// </summary>
    public readonly struct ClientEndpoint
    {
        public ClientEndpoint(string uri, ReadOnlyMemory<byte> credential, ReadOnlyMemory<byte> nonce, TimeSpan connectTimeout)
        {
            Uri = uri ?? string.Empty;
            Credential = credential;
            Nonce = nonce;
            ConnectTimeout = connectTimeout;
        }

        public string Uri { get; }

        public ReadOnlyMemory<byte> Credential { get; }

        public ReadOnlyMemory<byte> Nonce { get; }

        public TimeSpan ConnectTimeout { get; }

        /// <summary>LocalEmbedded 路径不带 endpoint;调用方据此区分环回与远程。</summary>
        public bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(Uri); }
        }

        /// <summary>只渲染长度,不渲染字节——日志必须脱敏凭据与认证材料。</summary>
        public override string ToString()
        {
            return string.Concat(
                IsConfigured ? Uri : "(unconfigured)",
                " (credential ",
                Credential.Length.ToString(CultureInfo.InvariantCulture),
                "B, nonce ",
                Nonce.Length.ToString(CultureInfo.InvariantCulture),
                "B, timeout ",
                ConnectTimeout.ToString(null, CultureInfo.InvariantCulture),
                ")");
        }
    }
}
