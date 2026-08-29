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
        private static readonly char[] UriTailMarkers = { '?', '#' };

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

        /// <summary>
        /// 远程传输拨号前的格式校验。<paramref name="reason"/> 只描述**形状**,永不回显凭据。
        /// </summary>
        /// <remarks>
        /// query / fragment / userinfo 一律拒绝:凭据只经 <c>Sec-WebSocket-Protocol</c> 的三段位序携带,
        /// URI 不是合法承载位。放行它们等于给「把 token 拼进 URL」留一条不被任何东西阻止的路。
        /// 同时容纳 <c>ws://</c> 与 <c>wss://</c>——A1-α 全程明文 loopback,但 CC-5 的 CLI 是
        /// <c>--transport ws|wss</c>,值类型不得只认一种。
        /// </remarks>
        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(Uri))
            {
                reason = "endpoint uri is empty";
                return false;
            }

            System.Uri parsed;
            if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out parsed))
            {
                reason = "endpoint uri is not an absolute uri";
                return false;
            }

            if (!string.Equals(parsed.Scheme, "ws", StringComparison.Ordinal)
                && !string.Equals(parsed.Scheme, "wss", StringComparison.Ordinal))
            {
                reason = "endpoint uri scheme must be ws or wss";
                return false;
            }

            if (!string.IsNullOrEmpty(parsed.UserInfo))
            {
                reason = "endpoint uri must not carry userinfo";
                return false;
            }

            if (!string.IsNullOrEmpty(parsed.Query))
            {
                reason = "endpoint uri must not carry a query; credentials ride the subprotocol";
                return false;
            }

            if (!string.IsNullOrEmpty(parsed.Fragment))
            {
                reason = "endpoint uri must not carry a fragment";
                return false;
            }

            if (Credential.IsEmpty)
            {
                reason = "endpoint credential must not be empty";
                return false;
            }

            if (Nonce.IsEmpty)
            {
                reason = "endpoint nonce must not be empty";
                return false;
            }

            if (ConnectTimeout <= TimeSpan.Zero)
            {
                reason = "endpoint connect timeout must be positive";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 只渲染长度,不渲染字节——日志必须脱敏凭据与认证材料。
        /// URI 也要脱敏:<see cref="TryValidate"/> 会拒绝带 query / userinfo 的 endpoint,
        /// 但值类型本身构造不设防,渲染这一侧必须独立成立(纵深防御)。
        /// </summary>
        public override string ToString()
        {
            return string.Concat(
                IsConfigured ? RedactUri(Uri) : "(unconfigured)",
                " (credential ",
                Credential.Length.ToString(CultureInfo.InvariantCulture),
                "B, nonce ",
                Nonce.Length.ToString(CultureInfo.InvariantCulture),
                "B, timeout ",
                ConnectTimeout.ToString(null, CultureInfo.InvariantCulture),
                ")");
        }

        /// <summary>
        /// 砍掉 query / fragment,抹掉 userinfo,保留 scheme + host + port + path。
        /// 用字符串手术而不是重建 <see cref="System.Uri"/>:重建会规范化默认端口,
        /// 把调用方原本写的 <c>:443</c> 悄悄改掉,反而降低诊断价值。
        /// </summary>
        internal static string RedactUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return "(unconfigured)";
            }

            int cut = uri.IndexOfAny(UriTailMarkers);
            string head = cut < 0 ? uri : uri.Substring(0, cut);
            string tail = cut < 0
                ? string.Empty
                : (uri[cut] == '?' ? "?<redacted>" : "#<redacted>");

            int schemeEnd = head.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd >= 0)
            {
                int authorityStart = schemeEnd + 3;
                int authorityEnd = head.IndexOf('/', authorityStart);
                if (authorityEnd < 0)
                {
                    authorityEnd = head.Length;
                }

                if (authorityEnd > authorityStart)
                {
                    int at = head.LastIndexOf('@', authorityEnd - 1);
                    if (at >= authorityStart)
                    {
                        head = string.Concat(
                            head.Substring(0, authorityStart),
                            "<redacted>@",
                            head.Substring(at + 1));
                    }
                }
            }

            return string.Concat(head, tail);
        }
    }
}
