using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Lumio.Client.Input;
using Lumio.Client.Replica;
using Lumio.Client.Session;
using Lumio.GameRuntime.Samples.Username.Components.Chat;

namespace Lumio.Client.Bot
{
    public sealed class HeadlessBotHost : IHeadlessBotHost
    {
        private readonly IClientSession _session;
        private readonly IBotScenarioDriver _driver;
        private readonly IInputSampleIngress _ingress;
        private readonly IBotTickHook _hook;
        private readonly ClientTimerManager? _timer;
        private readonly IReplicaWorld? _world;
        private readonly List<ulong> _submittedTicks = new List<ulong>();
        private bool _inputEnabled = true;
        private bool _reconnected;
        private string _inputStopReason = string.Empty;

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress)
            : this(session, driver, ingress, new NullTickHook(), null, null)
        {
        }

        public HeadlessBotHost(IClientSession session, IBotScenarioDriver driver, IInputSampleIngress ingress, IBotTickHook hook)
            : this(session, driver, ingress, hook, null, null)
        {
        }

        public HeadlessBotHost(
            IClientSession session,
            IBotScenarioDriver driver,
            IInputSampleIngress ingress,
            IBotTickHook hook,
            ClientTimerManager? timer)
            : this(session, driver, ingress, hook, timer, null)
        {
        }

        public HeadlessBotHost(
            IClientSession session,
            IBotScenarioDriver driver,
            IInputSampleIngress ingress,
            IBotTickHook hook,
            ClientTimerManager? timer,
            IReplicaWorld? world)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _ingress = ingress ?? throw new ArgumentNullException(nameof(ingress));
            _hook = hook ?? new NullTickHook();
            _timer = timer;
            _world = world;
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
                if (_session.GetSnapshot().State == ClientSessionState.Superseded)
                {
                    StopInput("connection_superseded");
                }

                if (_timer != null && _inputEnabled)
                {
                    IReadOnlyList<ulong> dues = _timer.Advance(tick);
                    for (int d = 0; d < dues.Count; d++)
                    {
                        if (_world != null)
                        {
                            TrySendChat(dues[d]);
                        }
                        else
                        {
                            int n = _driver.FillSamples(new BotDriverContext(i), samples);
                            for (int s = 0; s < n; s++)
                            {
                                _ingress.TryEnqueue(in samples[s]);
                            }
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
                if (_world == null)
                {
                    await Task.Yield();
                }
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

        private void TrySendChat(ulong dueTick)
        {
            if (_world == null || !_world.InputEnabled)
            {
                return;
            }

            try
            {
                _world.Manager.World.Self.Get<ChatComponent>().SendMessage("bot-" + dueTick.ToString(CultureInfo.InvariantCulture));
                _world.Manager.Tick();
            }
            catch (InvalidOperationException)
            {
            }
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
