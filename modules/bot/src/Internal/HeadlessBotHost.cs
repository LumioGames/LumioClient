using System;
using System.Collections.Generic;
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
        private readonly ClientTimerManager? _timer;
        private readonly List<ulong> _submittedTicks = new List<ulong>();
        private bool _inputEnabled = true;
        private bool _reconnected;
        private string _inputStopReason = string.Empty;

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress)
            : this(session, driver, ingress, new NullTickHook(), null)
        {
        }

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress, IBotTickHook hook)
            : this(session, driver, ingress, hook, null)
        {
        }

        public HeadlessBotHost(
            IClientSession session,
            IBotScenarioDriver driver,
            IInputSampleIngress ingress,
            IBotTickHook hook,
            ClientTimerManager? timer)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
            _hook = hook ?? new NullTickHook();
            _timer = timer;
        }

        public IReadOnlyList<ulong> SubmittedTicks
        {
            get { return _submittedTicks.ToArray(); }
        }

        public string InputStopReason
        {
            get { return _inputStopReason; }
        }

        public bool Reconnected
        {
            get { return _reconnected; }
        }

        public void StopInput(string reason)
        {
            _inputEnabled = false;
            _inputStopReason = reason ?? string.Empty;
        }

        public async Task<int> RunAsync(BotRunRequest request, CancellationToken cancellationToken)
        {
            _session.RequestConnect(new SessionConnectRequest(1), cancellationToken);
            if (_timer != null)
            {
                _timer.ScheduleBotChatCadence();
            }

            var samples = new RawInputSample[4];
            for (int i = 0; i < request.Ticks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _hook.BeforeTick(i);
                ulong tick = (ulong)(i + 1);
                if (_timer != null && _inputEnabled)
                {
                    IReadOnlyList<ulong> dues = _timer.Advance(tick);
                    for (int d = 0; d < dues.Count; d++)
                    {
                        int n = _driver.FillSamples(new BotDriverContext(i), samples);
                        for (int s = 0; s < n; s++)
                        {
                            _ingress.TryEnqueue(in samples[s]);
                        }

                        _submittedTicks.Add(dues[d]);
                    }
                }
                else if (_timer != null)
                {
                    _timer.Advance(tick);
                }

                _session.Tick(new ClientOwnerTick((ulong)i));
                _ = _session.GetSnapshot();
                await Task.Yield();
            }

            _session.RequestClose(new SessionCloseRequest(false));
            _reconnected = false;
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
