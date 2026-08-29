using System;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// 远程 WS 传输的 <see cref="IClientConnectionFactory"/> 实现(R-00260 §12.2 CC-2)。
    /// 与 <see cref="ClientConnectionFactory"/>(LocalEmbedded)并列:同一个 <see cref="IClientConnection"/>
    /// 上层语义,不同的物理通道;远程结果没有 loopback 端。
    /// </summary>
    /// <remarks>
    /// 本类型是 public 却落在 <c>Internal/Transport/WebSocket/</c> 目录下,成因见
    /// <see cref="WebSocketTransportOptions"/> 的说明:ADR 0003 裁决三把落点限定在该目录。
    /// </remarks>
    public sealed class WebSocketClientConnectionFactory : IClientConnectionFactory
    {
        private readonly WebSocketTransportOptions _options;
        private readonly ITransportFaultPolicy _faultPolicy;

        public WebSocketClientConnectionFactory()
            : this(WebSocketTransportOptions.Default)
        {
        }

        public WebSocketClientConnectionFactory(WebSocketTransportOptions options)
            : this(options, new PassThroughFaultPolicy())
        {
        }

        public WebSocketClientConnectionFactory(WebSocketTransportOptions options, ITransportFaultPolicy faultPolicy)
        {
            if (!options.TryValidate(out string reason))
            {
                throw new ArgumentException(reason, nameof(options));
            }

            _options = options;
            _faultPolicy = faultPolicy ?? new PassThroughFaultPolicy();
        }

        public ClientConnectionCreateResult Create(in ClientConnectionCreateRequest request, out IClientConnection connection)
        {
            if (!request.Endpoint.TryValidate(out string reason))
            {
                // Endpoint 格式不合法是「可拒绝」,必须在拨号之前挡住,并且是可观测的终态。
                connection = WebSocketClientConnection.Rejected(in request, _options, reason);
                return new ClientConnectionCreateResult(false);
            }

            connection = WebSocketClientConnection.Create(in request, _options, _faultPolicy);
            return new ClientConnectionCreateResult(true);
        }
    }
}
