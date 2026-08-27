using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Handshake
{
    internal sealed class HandshakeSession : IClientHandshake
    {
        private readonly IPlatformCapabilityProvider _capabilities;
        private readonly GeneratedHandshakeAdapter _adapter;
        private readonly CapabilityCompletionQueue _completions = new CapabilityCompletionQueue();
        private HandshakeAttemptId _attempt;
        private ulong _generation;
        private HandshakePhase _phase = HandshakePhase.Idle;
        private HandshakeRejectReason _reject;
        private bool _helloValid;
        private bool _capabilityReady;
        private bool _capabilityOk;

        public HandshakeSession(IPlatformCapabilityProvider capabilities)
            : this(capabilities, new UnpublishedHandshakeFrameClassifier())
        {
        }

        public HandshakeSession(IPlatformCapabilityProvider capabilities, IHandshakeFrameClassifier classifier)
        {
            _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
            _adapter = new GeneratedHandshakeAdapter(classifier);
        }

        public HandshakeCommandResult Begin(in HandshakeBeginRequest request)
        {
            if (_phase != HandshakePhase.Idle
                && _phase != HandshakePhase.Rejected
                && _phase != HandshakePhase.Cancelled
                && _phase != HandshakePhase.Accepted)
            {
                return new HandshakeCommandResult(false);
            }

            _attempt = request.Attempt;
            _generation = request.Generation;
            _phase = HandshakePhase.AwaitingHello;
            _reject = HandshakeRejectReason.None;
            _helloValid = false;
            _capabilityReady = false;
            _capabilityOk = false;
            return new HandshakeCommandResult(true);
        }

        public HandshakeCommandResult HandleFrame(ReadOnlyMemory<byte> frame)
        {
            if (_phase != HandshakePhase.AwaitingHello && _phase != HandshakePhase.AwaitingCapability)
            {
                return new HandshakeCommandResult(false);
            }

            HandshakeOpaqueFrameRole role = _adapter.Classify(frame);
            if (role == HandshakeOpaqueFrameRole.Unclassified)
            {
                return new HandshakeCommandResult(false);
            }

            if (role == HandshakeOpaqueFrameRole.HandshakeReject)
            {
                _phase = HandshakePhase.Rejected;
                _reject = HandshakeRejectReason.InvalidHello;
                return new HandshakeCommandResult(true);
            }

            if (role != HandshakeOpaqueFrameRole.ServerHello)
            {
                return new HandshakeCommandResult(false);
            }

            _helloValid = true;
            _phase = HandshakePhase.AwaitingCapability;
            ValueTask<PlatformCapabilityResult> pending = _capabilities.QueryAsync(
                new PlatformCapabilityQuery(_attempt, _generation),
                CancellationToken.None);
            if (pending.IsCompleted)
            {
                PlatformCapabilityResult result = pending.Result;
                _completions.Enqueue(in result);
            }
            return new HandshakeCommandResult(true);
        }

        public HandshakeOutcome Poll()
        {
            if (_completions.TryDequeue(out PlatformCapabilityResult result))
            {
                if (result.Attempt.Value != _attempt.Value || result.Generation != _generation)
                {
                    return GetSnapshot();
                }

                _capabilityReady = true;
                _capabilityOk = result.Compatible;
                if (_helloValid && _capabilityReady && _capabilityOk)
                {
                    _phase = HandshakePhase.Accepted;
                }
                else
                {
                    _phase = HandshakePhase.Rejected;
                    _reject = HandshakeRejectReason.CapabilityMismatch;
                }
            }

            return GetSnapshot();
        }

        public HandshakeCommandResult Cancel()
        {
            if (_phase == HandshakePhase.Accepted || _phase == HandshakePhase.Rejected)
            {
                if (_phase == HandshakePhase.Accepted)
                {
                    return new HandshakeCommandResult(false);
                }
            }

            if (_phase != HandshakePhase.Idle)
            {
                _phase = HandshakePhase.Cancelled;
                _reject = HandshakeRejectReason.Cancelled;
            }

            return new HandshakeCommandResult(true);
        }

        public HandshakeOutcome GetSnapshot()
        {
            return new HandshakeOutcome(_phase, _reject, _phase == HandshakePhase.Accepted);
        }
    }
}
