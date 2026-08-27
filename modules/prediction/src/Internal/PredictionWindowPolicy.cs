namespace Lumio.Client.Prediction
{
    internal sealed class PredictionWindowPolicy
    {
        private int _capacity;
        private int _highWatermark;

        public PredictionWindowPolicy(int capacity)
        {
            _capacity = capacity < 0 ? 0 : capacity;
        }

        public int Capacity
        {
            get { return _capacity; }
        }

        public int HighWatermark
        {
            get { return _highWatermark; }
        }

        public bool CanAccept(int occupancy)
        {
            return occupancy < _capacity;
        }

        public void NoteOccupancy(int occupancy)
        {
            if (occupancy > _highWatermark)
            {
                _highWatermark = occupancy;
            }
        }

        public void Reset(int capacity)
        {
            _capacity = capacity < 0 ? 0 : capacity;
            _highWatermark = 0;
        }
    }
}
