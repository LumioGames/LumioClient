using System;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// MVP 通道认证材料的**子协议位序承载**：Upgrade 期以三段
    /// <c>lumio.mvp.v0, &lt;opaqueTokenB64Url&gt;, &lt;opaqueNonceB64Url&gt;</c>
    /// 设置 <c>Sec-WebSocket-Protocol</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>该位序不是公共契约。</b>它是 LumioServer / LumioClient 的**双端私有约定**。
    /// 依据 <c>TRANSPORT-WEBSOCKET-PROFILE-REGISTRATION.md</c> §3 原文「清单外的一切都不是公共契约」——
    /// 该文点名 WS 子协议名、端点路径与 close code 映射归 Server / Client 自行约定。
    /// LumioServer 侧把同一件事登记为 <c>mvp-host/absences.json</c> 的 <c>ABS-AUTH-CREDENTIAL-CARRIAGE</c>。
    /// </para>
    /// <para>
    /// <b>退场纪律</b>：架构源冻结凭据承载方式（<b>D-011</b>）后，即改用公共形态并**删除本约定**。
    /// <c>lumio.mvp.v0</c> 里的 <c>mvp</c> 与 <c>v0</c> 是**退场标记，不得去掉**——它们是这段私有
    /// 约定在代码里唯一的到期提示。
    /// </para>
    /// <para>
    /// 本层只搬运不透明字节：不定义凭据格式、算法、轮换，也不派生 nonce。
    /// </para>
    /// </remarks>
    internal static class MvpChannelAuth
    {
        /// <summary>协商成功后 <c>ClientWebSocket.SubProtocol</c> 必须恰为此值。</summary>
        internal const string SubProtocol = "lumio.mvp.v0";

        /// <summary>
        /// base64url,**不带 padding**。`=` 不是 RFC 7230 token 的合法字符,
        /// <c>ClientWebSocket.Options.AddSubProtocol</c> 会直接拒绝带 padding 的段。
        /// </summary>
        internal static string ToBase64Url(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
            {
                return string.Empty;
            }

            return Convert.ToBase64String(bytes.ToArray())
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
