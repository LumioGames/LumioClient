using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Properties;

public sealed class InputSequenceProperties
{
    [Fact]
    public void AcceptedSeqIsMonotonicAcrossManyEnqueues()
    {
        var ingress = new InputSampleIngress(capacity: 64);
        InputSampleSeq previous = default;
        for (int i = 0; i < 32; i++)
        {
            var receipt = ingress.TryEnqueue(new RawInputSample((uint)i, 0, 0));
            Assert.True(receipt.Accepted);
            if (i > 0)
            {
                Assert.True(receipt.Sequence.Value > previous.Value);
            }

            previous = receipt.Sequence;
        }
    }
}
