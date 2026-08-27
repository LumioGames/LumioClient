namespace Lumio.Client.Prediction
{
    public interface IClientPredictionFactory
    {
        IClientPrediction Create(in PredictionCreateRequest request);
    }

    public readonly struct PredictionCreateRequest
    {
        public PredictionCreateRequest(ulong generation, int windowCapacity)
        {
            Generation = generation;
            WindowCapacity = windowCapacity;
        }

        public ulong Generation { get; }

        public int WindowCapacity { get; }
    }

    public sealed class ClientPredictionFactory : IClientPredictionFactory
    {
        public IClientPrediction Create(in PredictionCreateRequest request)
        {
            int capacity = request.WindowCapacity;
            if (capacity < 0)
            {
                capacity = 0;
            }

            return new ClientPrediction(request.Generation, capacity);
        }
    }
}
