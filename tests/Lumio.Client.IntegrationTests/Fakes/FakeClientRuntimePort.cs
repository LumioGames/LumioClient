using Lumio.Client.Session;

namespace Lumio.Client.IntegrationTests.Fakes;

public enum FakeRuntimeTransactionKind
{
    Authoritative,
    LocalPrediction
}

public readonly struct FakeRuntimeTransactionRequest
{
    public FakeRuntimeTransactionRequest(FakeRuntimeTransactionKind kind, ulong generation, ReadOnlyMemory<byte> opaquePlan)
    {
        Kind = kind;
        Generation = generation;
        OpaquePlan = opaquePlan;
    }

    public FakeRuntimeTransactionKind Kind { get; }

    public ulong Generation { get; }

    public ReadOnlyMemory<byte> OpaquePlan { get; }
}

public readonly struct FakeRuntimeTransactionOutcome
{
    public FakeRuntimeTransactionOutcome(bool committed, string code)
    {
        Committed = committed;
        Code = code;
    }

    public bool Committed { get; }

    public string Code { get; }
}

public sealed class FakeClientRuntimePort : IClientRuntimePort
{
    private readonly List<FakeRuntimeTransactionRequest> _calls = new();
    private FakeRuntimeTransactionOutcome _next = new(true, "ok");

    public IReadOnlyList<FakeRuntimeTransactionRequest> Calls => _calls;

    public int ApplyAuthoritativeTransactionCalls => _calls.Count(c => c.Kind == FakeRuntimeTransactionKind.Authoritative);

    public int ApplyLocalPredictionCalls => _calls.Count(c => c.Kind == FakeRuntimeTransactionKind.LocalPrediction);

    public void InjectOutcome(FakeRuntimeTransactionOutcome outcome)
    {
        _next = outcome;
    }

    public FakeRuntimeTransactionOutcome ApplyAuthoritativeTransaction(in FakeRuntimeTransactionRequest request)
    {
        _calls.Add(request);
        return _next;
    }

    public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FakeRuntimeTransactionOutcome fake = ApplyAuthoritativeTransaction(
            new FakeRuntimeTransactionRequest(FakeRuntimeTransactionKind.Authoritative, request.Generation, request.OpaquePlan));
        return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(fake.Committed));
    }

    public ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FakeRuntimeTransactionOutcome fake = ApplyAuthoritativeTransaction(
            new FakeRuntimeTransactionRequest(FakeRuntimeTransactionKind.LocalPrediction, request.Generation, request.OpaquePlan));
        return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(fake.Committed));
    }
}
