using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Session
{
    public readonly struct RuntimeTransactionRequest
    {
        public RuntimeTransactionRequest(ulong generation, ReadOnlyMemory<byte> opaquePlan)
        {
            Generation = generation;
            OpaquePlan = opaquePlan;
        }

        public ulong Generation { get; }

        public ReadOnlyMemory<byte> OpaquePlan { get; }
    }

    public readonly struct RuntimeTransactionOutcome
    {
        public RuntimeTransactionOutcome(bool committed)
            : this(committed, false)
        {
        }

        public RuntimeTransactionOutcome(bool committed, bool indeterminate)
        {
            if (indeterminate)
            {
                Committed = false;
                Indeterminate = true;
            }
            else
            {
                Committed = committed;
                Indeterminate = false;
            }
        }

        public bool Committed { get; }

        public bool Indeterminate { get; }

        public static RuntimeTransactionOutcome IndeterminateOutcome()
        {
            return new RuntimeTransactionOutcome(false, true);
        }
    }

    public interface IClientRuntimePort
    {
        ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(
            in RuntimeTransactionRequest request,
            CancellationToken cancellationToken);

        ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(
            in RuntimeTransactionRequest request,
            CancellationToken cancellationToken);
    }
}
