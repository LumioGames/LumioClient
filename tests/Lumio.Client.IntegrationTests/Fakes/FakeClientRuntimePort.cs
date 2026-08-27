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

public sealed class FakeClientRuntimePort
{
    private readonly List<FakeRuntimeTransactionRequest> _calls = new();
    private FakeRuntimeTransactionOutcome _next = new(true, "ok");

    public IReadOnlyList<FakeRuntimeTransactionRequest> Calls => _calls;

    public int ApplyAuthoritativeTransactionCalls => _calls.Count(c => c.Kind == FakeRuntimeTransactionKind.Authoritative);

    public void InjectOutcome(FakeRuntimeTransactionOutcome outcome)
    {
        _next = outcome;
    }

    public FakeRuntimeTransactionOutcome ApplyAuthoritativeTransaction(in FakeRuntimeTransactionRequest request)
    {
        _calls.Add(request);
        return _next;
    }
}
