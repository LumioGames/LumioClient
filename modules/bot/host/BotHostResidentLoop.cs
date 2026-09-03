using Lumio.Client.Bot;
using Lumio.Client.Replica;
using Lumio.Client.Session;
using Lumio.GameRuntime.Samples.Username.Components.Chat;

namespace Lumio.Client.Bot.Host;

internal readonly struct ResidentBot
{
    public ResidentBot(string accountId, IClientSession session)
    {
        AccountId = accountId;
        Session = session;
    }

    public string AccountId { get; }

    public IClientSession Session { get; }
}

internal static class BotHostResidentLoop
{
    public static async Task RunAsync(
        IReadOnlyList<ResidentBot> bots,
        ClientTimerManager timer,
        string logPath,
        string releaseFlag,
        Func<CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        ulong tick = 0;
        while (!cancellationToken.IsCancellationRequested && !File.Exists(releaseFlag))
        {
            tick++;
            for (int i = 0; i < bots.Count; i++)
            {
                bots[i].Session.Tick(new ClientOwnerTick(tick));
            }

            IReadOnlyList<ulong> dues = timer.Advance(tick);
            for (int d = 0; d < dues.Count; d++)
            {
                for (int i = 0; i < bots.Count; i++)
                {
                    ResidentBot bot = bots[i];
                    if (!bot.Session.TryGetReplicaWorld(out IReplicaWorld world) || !world.InputEnabled)
                    {
                        continue;
                    }

                    world.Manager.World.Self.Get<ChatComponent>().SendMessage(
                        "bot-" + dues[d].ToString(System.Globalization.CultureInfo.InvariantCulture));
                    world.Manager.Tick();
                    AppendChatInputLog(logPath, dues[d], bot.AccountId);
                }
            }

            await delay(cancellationToken);
        }
    }

    public static void AppendChatInputLog(string path, ulong tick, string accountId)
    {
        string line = "{\"ts\":\"" + DateTime.UtcNow.ToString("o") +
                      "\",\"kind\":\"chat.input\",\"tick\":" + tick.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"tickSource\":\"native-kernel/tickFrame\",\"pid\":" +
                      Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"accountId\":\"" + accountId + "\"}\n";
        File.AppendAllText(path, line);
    }
}
