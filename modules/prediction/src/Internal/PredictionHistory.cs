using System.Collections.Generic;

namespace Lumio.Client.Prediction
{
    internal sealed class PredictionHistory
    {
        private readonly List<AcceptedPredictionCommand> _commands = new List<AcceptedPredictionCommand>();

        public int Count
        {
            get { return _commands.Count; }
        }

        public void Append(in AcceptedPredictionCommand command)
        {
            _commands.Add(command);
        }

        public int UnconfirmedCountAfter(ulong confirmedThrough)
        {
            int count = 0;
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].CommandSeq.Value > confirmedThrough)
                {
                    count++;
                }
            }

            return count;
        }

        public void PruneThrough(ulong confirmedThrough)
        {
            int remove = 0;
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].CommandSeq.Value <= confirmedThrough)
                {
                    remove++;
                    continue;
                }

                break;
            }

            if (remove > 0)
            {
                _commands.RemoveRange(0, remove);
            }
        }

        public void Clear()
        {
            _commands.Clear();
        }
    }
}
