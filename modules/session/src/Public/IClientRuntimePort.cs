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
        {
            Committed = committed;
        }

        public bool Committed { get; }
    }

    public interface IClientRuntimePort
    {
        ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(
            in RuntimeTransactionRequest request,
            CancellationToken cancellationToken);
    }
}
