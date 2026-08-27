using Lumio.Client.Replica;
using Lumio.Client.Replica.Tests.Unit;

namespace Lumio.Client.Replica.Tests.Contract;

public sealed class ReplicaMapperContractTests
{
    [Fact]
    public void ValidUpdateProducesOneImmutableRuntimePlan()
    {
        var mapper = new RuntimeReplicaPlanAdapter();
        var bytes = new byte[] { 1, 4, 8 };
        ReplicaStageRequest request = ReplicaRequests.FullSnapshot(1, 10, 1, 1, update: bytes);
        var context = new ReplicaMappingContext(1, 0, 0);

        ReplicaMappingResult first = mapper.Map(in request, in context, out ReadOnlyMemory<byte> plan1);
        bytes[0] = 9;
        ReplicaMappingResult second = mapper.Map(
            ReplicaRequests.FullSnapshot(1, 10, 1, 1, update: new byte[] { 1, 4, 8 }),
            in context,
            out ReadOnlyMemory<byte> plan2);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, CountPlans(plan1));
        Assert.Equal(1, CountPlans(plan2));
        Assert.True(plan1.Span.SequenceEqual(new byte[] { 1, 4, 8 }));
        Assert.True(plan1.Span.SequenceEqual(plan2.Span));
    }

    [Fact]
    public void InvalidFixtureHasZeroRuntimePlan()
    {
        var mapper = new RuntimeReplicaPlanAdapter();
        var context = new ReplicaMappingContext(1, 0, 0);
        ReplicaMappingResult empty = mapper.Map(
            ReplicaRequests.FullSnapshot(1, 10, 1, 1, update: Array.Empty<byte>()),
            in context,
            out ReadOnlyMemory<byte> emptyPlan);
        ReplicaMappingResult opaque = mapper.Map(
            ReplicaRequests.Delta(1, 10, 1, 2, 2, update: new byte[] { 0 }),
            in context,
            out ReadOnlyMemory<byte> opaquePlan);

        Assert.False(empty.Succeeded);
        Assert.True(opaque.Succeeded);
        Assert.Equal(0, CountPlans(emptyPlan));
        Assert.Equal(1, CountPlans(opaquePlan));
        Assert.True(emptyPlan.IsEmpty);
        Assert.False(opaquePlan.IsEmpty);
    }

    private static int CountPlans(ReadOnlyMemory<byte> plan)
    {
        return plan.IsEmpty ? 0 : 1;
    }
}
