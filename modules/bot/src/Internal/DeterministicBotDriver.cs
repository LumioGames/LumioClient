using Lumio.Client.Input;

namespace Lumio.Client.Bot
{
    public sealed class DeterministicBotDriver : IBotScenarioDriver
    {
        public int FillSamples(in BotDriverContext context, System.Span<RawInputSample> destination)
        {
            if (destination.Length == 0)
            {
                return 0;
            }

            destination[0] = new RawInputSample((uint)context.Tick, 0, 0);
            return 1;
        }
    }
}
