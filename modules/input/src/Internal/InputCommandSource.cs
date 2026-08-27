using System;

namespace Lumio.Client.Input
{
    public sealed class InputCommandSource : IInputCommandSource
    {
        private readonly IInputSampleIngress _ingress;
        private readonly IGameInputMapper _mapper;
        private readonly InputBufferPolicyState _policy = new InputBufferPolicyState();

        public InputCommandSource(IInputSampleIngress ingress, IGameInputMapper mapper)
        {
            _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public int DrainCandidates(Span<GameplayCommandCandidate> destination, in InputDrainContext context)
        {
            SequencedInputSample[] accepted = _ingress.DrainAccepted();
            int written = 0;
            int limit = Math.Min(destination.Length, context.MaxCandidates);
            for (int i = 0; i < accepted.Length && written < limit; i++)
            {
                if (_policy.Current.Kind == InputBufferPolicyKind.Drop && _policy.AppliesTo(context.Generation))
                {
                    continue;
                }

                try
                {
                    if (_mapper.TryMap(in accepted[i], in context, out GameplayCommandCandidate candidate))
                    {
                        if (candidate.ClientCommandSeq.HasValue)
                        {
                            continue;
                        }

                        destination[written] = candidate;
                        written++;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return written;
        }

        public void SetBufferPolicy(in InputBufferPolicy policy)
        {
            _policy.Set(in policy);
        }

        public InputBufferPolicy GetSnapshotPolicy()
        {
            return _policy.Current;
        }
    }
}
