using Lumio.Client.Input;

namespace Lumio.Client.Session
{
    internal sealed class ResyncOrchestrator
    {
        private readonly bool _owned = true;

        public void Enter(IInputCommandSource commands, ulong generation)
        {
            if (!_owned)
            {
                return;
            }

            commands.SetBufferPolicy(new InputBufferPolicy(InputBufferPolicyKind.Resync, generation));
        }
    }
}
