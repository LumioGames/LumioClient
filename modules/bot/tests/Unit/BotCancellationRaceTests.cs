using Lumio.Client.Bot;
using Lumio.Client.Input;

namespace Lumio.Client.Bot.Tests.Unit;

public sealed class BotCancellationRaceTests
{
    [Fact]
    public async Task BotCancellation_Stops()
    {
        var ingress = new InputSampleIngress(8);
        var host = new HeadlessBotHost(BotSessionFactory.Create(ingress), new DeterministicBotDriver(), ingress);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.RunAsync(new BotRunRequest(10, 0), cts.Token));
    }

    [Fact]
    public async Task CancelDuringRun_ThrowsAndDoesNotHang()
    {
        var ingress = new InputSampleIngress(8);
        var host = new HeadlessBotHost(BotSessionFactory.Create(ingress), new DeterministicBotDriver(), ingress);
        using var cts = new CancellationTokenSource();
        Task<int> running = host.RunAsync(new BotRunRequest(10_000, 0), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => running);
    }
}
