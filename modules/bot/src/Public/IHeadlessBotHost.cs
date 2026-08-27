using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Bot
{
    public readonly struct BotRunRequest
    {
        public BotRunRequest(int ticks, int exitCode)
        {
            Ticks = ticks;
            ExitCode = exitCode;
        }

        public int Ticks { get; }

        public int ExitCode { get; }
    }

    public interface IHeadlessBotHost
    {
        Task<int> RunAsync(in BotRunRequest request, CancellationToken cancellationToken);
    }
}
