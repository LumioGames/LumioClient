using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// 走 <c>ClientWebSocket</c> 的远程传输,与 LocalEmbedded 共用同一套 <see cref="IClientConnection"/> 语义。
    /// 一个 WS 消息 = 一个完整 Envelope;本层只搬运不透明字节,不理解 Envelope 内容。
    /// </summary>
    /// <remarks>
    /// 拨号侧归 Client,监听侧归 Server:本类只 connect,永不 listen。
    /// 走 <c>ClientWebSocket</c> 而不是 BCL Socket / SslStream / Pipelines,依据 ADR 0003 裁决四——
    /// 后三者已在 <c>eng/BannedSymbols.txt</c>,生产工程内使用会 <c>RS0030</c> 构建失败。
    /// </remarks>
    internal sealed class WebSocketClientConnection : IClientConnection, IDisposable
    {
        private readonly object _gate = new object();
        private readonly ConnectionStateMachine _machine;
        private readonly ConnectionSendQueue _sendQueue;
        private readonly FaultDecoratingTransport _faults;
        private readonly ClientEndpoint _endpoint;
        private readonly WebSocketTransportOptions _options;
        private readonly int _drainLimit;
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        private readonly ManualResetEventSlim _openSignal = new ManualResetEventSlim(false);

        private ClientWebSocket? _socket;
        private Task? _pump;
        private Task? _sender;
        private bool _openSucceeded;
        private bool _disposed;

        private string? _negotiatedSubProtocol;
        private int _largestReceiveAllocationBytes;
        private long _applicationBytesReceived;
        private int _inboundDroppedByQueueFull;
        private bool _channelAuthRejected;
        private bool _idleDeadlineExpired;
        private bool _oversizeRejected;

        private WebSocketClientConnection(
            in ClientConnectionCreateRequest request,
            WebSocketTransportOptions options,
            ITransportFaultPolicy faultPolicy,
            string? rejection)
        {
            _endpoint = request.Endpoint;
            _options = options;
            _drainLimit = Math.Max(request.DrainLimit, 1);
            _faults = new FaultDecoratingTransport(faultPolicy ?? new PassThroughFaultPolicy());
            int capacity = Math.Max(request.EventCapacity, 1);
            _machine = new ConnectionStateMachine(request.Generation, capacity);
            _sendQueue = new ConnectionSendQueue(capacity);
            _socket = null;
            _pump = null;
            _sender = null;
            _negotiatedSubProtocol = null;
            RejectionReason = rejection;

            if (rejection != null)
            {
                // Endpoint 格式不合法属「可拒绝」:出生即终态,调用方 drain 得到一条 Faulted。
                _machine.TryClose(ConnectionCloseReason.Fault);
                _openSignal.Set();
            }
        }

        internal static WebSocketClientConnection Create(
            in ClientConnectionCreateRequest request,
            WebSocketTransportOptions options,
            ITransportFaultPolicy faultPolicy)
        {
            return new WebSocketClientConnection(in request, options, faultPolicy, null);
        }

        internal static WebSocketClientConnection Rejected(
            in ClientConnectionCreateRequest request,
            WebSocketTransportOptions options,
            string reason)
        {
            return new WebSocketClientConnection(in request, options, new PassThroughFaultPolicy(), reason);
        }

        // ---------- 诊断面(internal:不穿模块公共边界) ----------

        internal string? RejectionReason { get; }

        internal string? NegotiatedSubProtocol
        {
            get { lock (_gate) { return _negotiatedSubProtocol; } }
        }

        internal int LargestReceiveAllocationBytes
        {
            get { lock (_gate) { return _largestReceiveAllocationBytes; } }
        }

        internal long ApplicationBytesReceived
        {
            get { lock (_gate) { return _applicationBytesReceived; } }
        }

        internal int InboundDroppedByQueueFull
        {
            get { lock (_gate) { return _inboundDroppedByQueueFull; } }
        }

        internal bool ChannelAuthRejected
        {
            get { lock (_gate) { return _channelAuthRejected; } }
        }

        internal bool IdleDeadlineExpired
        {
            get { lock (_gate) { return _idleDeadlineExpired; } }
        }

        internal bool OversizeRejected
        {
            get { lock (_gate) { return _oversizeRejected; } }
        }

        /// <summary>诊断行。Endpoint 渲染已脱敏,凭据与 nonce 只出现长度。</summary>
        internal string DescribeForDiagnostics()
        {
            lock (_gate)
            {
                return string.Concat(
                    "websocket connection generation ",
                    _machine.Generation.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ", endpoint ",
                    _endpoint.ToString(),
                    ", subprotocol ",
                    _negotiatedSubProtocol ?? "(unnegotiated)",
                    ", terminal ",
                    _machine.Terminal ? "yes" : "no");
            }
        }

        internal bool WaitForOpen(TimeSpan timeout)
        {
            if (!_openSignal.Wait(timeout))
            {
                return false;
            }

            lock (_gate)
            {
                return _openSucceeded;
            }
        }

        /// <summary>迟到回调的代次隔离:代次不匹配或已终态一律不接受。</summary>
        internal bool DeliverCallback(ConnectionGeneration generation)
        {
            lock (_gate)
            {
                return _machine.TryDeliverLate(generation);
            }
        }

        // ---------- IClientConnection ----------

        public ConnectionGeneration Generation
        {
            get { lock (_gate) { return _machine.Generation; } }
        }

        public ConnectionCommandResult Start()
        {
            lock (_gate)
            {
                ConnectionCommandResult result = _machine.Start();
                if (!result.Succeeded)
                {
                    return result;
                }
            }

            _pump = Task.Run(() => RunAsync());
            return new ConnectionCommandResult(true);
        }

        public ConnectionSendResult TrySend(in EncodedFrame frame)
        {
            lock (_gate)
            {
                if (!_machine.CanSend(in frame))
                {
                    return new ConnectionSendResult(false);
                }

                if (!_sendQueue.TryEnqueue(in frame))
                {
                    // QueueFull:明确拒绝,不静默覆盖。
                    return new ConnectionSendResult(false);
                }
            }

            _sendSignal.Release();
            return new ConnectionSendResult(true);
        }

        public int DrainEvents(Span<ConnectionEvent> destination)
        {
            lock (_gate)
            {
                int limit = Math.Min(destination.Length, _drainLimit);
                return limit <= 0 ? 0 : _machine.Drain(destination.Slice(0, limit));
            }
        }

        public ConnectionCommandResult RequestClose(ConnectionCloseReason reason)
        {
            bool closed;
            lock (_gate)
            {
                closed = _machine.TryClose(reason);
            }

            if (closed)
            {
                _openSignal.Set();
                BeginShutdown();
            }

            return new ConnectionCommandResult(closed);
        }

        public ClientConnectionSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new ClientConnectionSnapshot(_machine.Generation, _machine.Terminal, _machine.EventCount);
            }
        }

        // ---------- 拨号与收发 ----------

        private async Task RunAsync()
        {
            ClientWebSocket socket = new ClientWebSocket();
            lock (_gate)
            {
                _socket = socket;
            }

            try
            {
                // 三段位序,顺序即契约(见 MvpChannelAuth 的退场纪律)。
                socket.Options.AddSubProtocol(MvpChannelAuth.SubProtocol);
                socket.Options.AddSubProtocol(MvpChannelAuth.ToBase64Url(_endpoint.Credential.Span));
                socket.Options.AddSubProtocol(MvpChannelAuth.ToBase64Url(_endpoint.Nonce.Span));

                using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token))
                {
                    connectCts.CancelAfter(_endpoint.ConnectTimeout);
                    await socket.ConnectAsync(new Uri(_endpoint.Uri), connectCts.Token).ConfigureAwait(false);
                }

                if (!string.Equals(socket.SubProtocol, MvpChannelAuth.SubProtocol, StringComparison.Ordinal))
                {
                    // 协商结果不是双端约定的那个值 —— 不发任何应用数据,直接终止。
                    Terminate(ConnectionCloseReason.Fault);
                    return;
                }

                lock (_gate)
                {
                    _negotiatedSubProtocol = socket.SubProtocol;
                    _openSucceeded = true;
                }

                _openSignal.Set();
                _sender = Task.Run(() => RunSendLoopAsync(socket, _shutdown.Token));
                await ReceiveLoopAsync(socket, _shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 拨号 / 收发的任何失败都归「传输报告断开事实」,是否重试由 session 决定。
                Terminate(ConnectionCloseReason.Fault);
            }
            finally
            {
                _openSignal.Set();
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[_options.ReceiveBufferBytes];
            var assembler = new WebSocketMessageAssembler(_options.MaxMessageBytes);

            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using (var idleCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    idleCts.CancelAfter(_options.IdleTimeout);
                    try
                    {
                        result = await socket
                            .ReceiveAsync(new ArraySegment<byte>(buffer), idleCts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        // 断线来源三之三:空闲截止到期。
                        lock (_gate)
                        {
                            _idleDeadlineExpired = true;
                        }

                        Terminate(ConnectionCloseReason.Disconnect);
                        return;
                    }
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // 断线来源三之一:对端发 close 帧。
                    // close 1008 是双端私有的通道认证拒绝语义,**不是公共错误码**
                    // (架构源的 ErrorCode 里没有「凭据无效」);本卡不新增任何 ErrorCode。
                    bool policyViolation = socket.CloseStatus == WebSocketCloseStatus.PolicyViolation;
                    if (policyViolation)
                    {
                        lock (_gate)
                        {
                            _channelAuthRejected = true;
                        }
                    }

                    Terminate(policyViolation ? ConnectionCloseReason.Fault : ConnectionCloseReason.Disconnect);
                    return;
                }

                if (!assembler.TryAppend(buffer, result.Count))
                {
                    // 超限:在分配前拒绝,当场中止读取并关闭。临时口径归
                    // errorClass = Rejectable / reasonCode = BudgetExceeded(R-00258 §4),不新增错误码。
                    lock (_gate)
                    {
                        _oversizeRejected = true;
                        _largestReceiveAllocationBytes = Math.Max(
                            _largestReceiveAllocationBytes, assembler.LargestAllocationBytes);
                    }

                    Terminate(ConnectionCloseReason.Fault);
                    return;
                }

                if (!result.EndOfMessage)
                {
                    // 一 WS 消息 = 一 Envelope:只有 EndOfMessage 为真才交付。
                    continue;
                }

                byte[] complete = assembler.Complete();
                lock (_gate)
                {
                    _largestReceiveAllocationBytes = Math.Max(
                        _largestReceiveAllocationBytes, assembler.LargestAllocationBytes);
                    _applicationBytesReceived += complete.Length;
                    if (!_machine.TryDeliverInbound(new EncodedFrame(complete)))
                    {
                        _inboundDroppedByQueueFull++;
                    }
                }
            }

            if (!token.IsCancellationRequested)
            {
                // 断线来源三之二:底层把连接推出 Open(含 ReceiveAsync 抛出后的中止态)。
                Terminate(ConnectionCloseReason.Fault);
            }
        }

        private async Task RunSendLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(token).ConfigureAwait(false);
                    while (TryTakeForSend(out EncodedFrame frame, out TransportFaultAction action))
                    {
                        if (action == TransportFaultAction.Drop)
                        {
                            continue;
                        }

                        await SendOneAsync(socket, frame, token).ConfigureAwait(false);
                        if (action == TransportFaultAction.Duplicate)
                        {
                            await SendOneAsync(socket, frame, token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                Terminate(ConnectionCloseReason.Fault);
            }
        }

        private static Task SendOneAsync(ClientWebSocket socket, EncodedFrame frame, CancellationToken token)
        {
            byte[] payload = frame.Bytes.ToArray();
            // 出站用 Text:与 LumioServer 的对称载体卡同口径(一消息一 Envelope,EndOfMessage 恒真)。
            return socket.SendAsync(
                new ArraySegment<byte>(payload),
                WebSocketMessageType.Text,
                true,
                token);
        }

        private bool TryTakeForSend(out EncodedFrame frame, out TransportFaultAction action)
        {
            lock (_gate)
            {
                if (_machine.Terminal || !_sendQueue.TryPeek(out frame))
                {
                    frame = default(EncodedFrame);
                    action = TransportFaultAction.Pass;
                    return false;
                }

                action = _faults.Next(0);
                _sendQueue.TryDequeue(out _);
                return true;
            }
        }

        private void Terminate(ConnectionCloseReason reason)
        {
            bool closed;
            lock (_gate)
            {
                closed = _machine.TryClose(reason);
            }

            _openSignal.Set();
            if (closed)
            {
                BeginShutdown();
            }
        }

        private void BeginShutdown()
        {
            ClientWebSocket? socket;
            lock (_gate)
            {
                socket = _socket;
            }

            if (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    _shutdown.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }

            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Abort();
            }
            catch (Exception)
            {
                // 关停竞态:通道已经没了就没什么好关的。
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            BeginShutdown();
            WaitForPumps();

            ClientWebSocket? socket;
            lock (_gate)
            {
                socket = _socket;
                _socket = null;
            }

            socket?.Dispose();
            _shutdown.Dispose();
            _sendSignal.Dispose();
            _openSignal.Dispose();
            GC.SuppressFinalize(this);
        }

        private void WaitForPumps()
        {
            Task? pump = _pump;
            Task? sender = _sender;
            try
            {
                pump?.Wait(TimeSpan.FromSeconds(5));
                sender?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // 关停期的异常不是被测行为。
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
