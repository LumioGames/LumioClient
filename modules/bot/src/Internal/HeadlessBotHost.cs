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
        private readonly IBotTickHook _hook;

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress)
            : this(session, driver, ingress, new NullTickHook())
        {
        }

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress, IBotTickHook hook)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
            _hook = hook ?? new NullTickHook();
        }

        public async Task<int> RunAsync(BotRunRequest request, CancellationToken cancellationToken)
        {
            _session.RequestConnect(new SessionConnectRequest(1), cancellationToken);
            var samples = new RawInputSample[4];
            for (int i = 0; i < request.Ticks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _hook.BeforeTick(i);

                int n = _driver.FillSamples(new BotDriverContext(i), samples);
                for (int s = 0; s < n; s++)
                {
                    _ingress.TryEnqueue(in samples[s]);
                }

                _session.Tick(new ClientOwnerTick((ulong)i));
                _ = _session.GetSnapshot();
                await Task.Yield();
            }

            _session.RequestClose(new SessionCloseRequest(false));
            ClientSessionState state = _session.GetSnapshot().State;
            if (state == ClientSessionState.Faulted)
            {
                return 1;
            }

            return 0;
        }

        Task<int> IHeadlessBotHost.RunAsync(in BotRunRequest request, CancellationToken cancellationToken)
        {
            return RunAsync(request, cancellationToken);
        }

        private sealed class NullTickHook : IBotTickHook
        {
            public void BeforeTick(int tick)
            {
                _ = tick;
            }
        }
    }
}
