using Lumio.Client.Bot.Host;
using Lumio.Client.Session;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationExitTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public async Task HeadlessBot_ConnectHandshakeTickClose()
    {
        int code = await FoundationHostCommand.RunAsync(
            new[] { "foundation", "--transport", "local-embedded", "--fixture", "foundation-happy-path" },
            CancellationToken.None);
        Assert.Equal(0, code);
    }
}
