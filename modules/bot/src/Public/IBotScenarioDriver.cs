using Lumio.Client.Input;

namespace Lumio.Client.Bot
{
    public readonly struct BotDriverContext
    {
        public BotDriverContext(int tick)
        {
            Tick = tick;
        }

        public int Tick { get; }
    }

    public interface IBotScenarioDriver
    {
        int FillSamples(in BotDriverContext context, System.Span<RawInputSample> destination);
    }

    public interface IBotTickHook
    {
        void BeforeTick(int tick);
    }
}
