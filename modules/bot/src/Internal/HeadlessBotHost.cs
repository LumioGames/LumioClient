using System;
using System.Threading;
using System.Threading.Tasks;
using Lumio.Client.Input;
using Lumio.Client.Session;

namespace Lumio.Client.Bot
{
    public sealed class HeadlessBotHost : IHeadlessBotHost
    {
        private readonly IClientSession _session;
        private readonly IBotScenarioDriver _driver;
        private readonly IInputSampleIngress _ingress;

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
        }

        public async Task<int> RunAsync(BotRunRequest request, CancellationToken cancellationToken)
        {
            _session.RequestConnect(new SessionConnectRequest(1), cancellationToken);
            var samples = new RawInputSample[4];
            for (int i = 0; i < request.Ticks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int n = _driver.FillSamples(new BotDriverContext(i), samples);
                for (int s = 0; s < n; s++)
                {
                    _ingress.TryEnqueue(in samples[s]);
                }

                _session.Tick(new ClientOwnerTick((ulong)i));
                await Task.Yield();
            }

            _session.RequestClose(new SessionCloseRequest(false));
            return request.ExitCode;
        }

        Task<int> IHeadlessBotHost.RunAsync(in BotRunRequest request, CancellationToken cancellationToken)
        {
            return RunAsync(request, cancellationToken);
        }
    }
}
