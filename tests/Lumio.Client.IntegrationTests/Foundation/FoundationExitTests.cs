using Lumio.Client.Bot;
using Lumio.Client.Session;
using Lumio.Client.IntegrationTests.Support;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationExitTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public async Task HeadlessBot_ConnectHandshakeTickClose()
    {
        var harness = new FoundationHarness(true);
        var host = new HeadlessBotHost(harness.Session, new DeterministicBotDriver(), harness.Ingress);
        int code = await host.RunAsync(new BotRunRequest(2, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(ClientSessionState.Closed, harness.Session.GetSnapshot().State);
    }
}
