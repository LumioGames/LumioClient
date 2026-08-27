namespace Lumio.Client.IntegrationTests.Fakes;

public sealed class FakeGameplayScopeActivator
{
    public int PrepareCalls { get; private set; }

    public int ActivateCalls { get; private set; }

    public int ReleaseCalls { get; private set; }

    public bool Prepared { get; private set; }

    public bool Activated { get; private set; }

    public Task PrepareAsync()
    {
        PrepareCalls++;
        Prepared = true;
        return Task.CompletedTask;
    }

    public void ActivateAtTickBarrier()
    {
        if (!Prepared)
        {
            throw new InvalidOperationException("scope is not prepared");
        }

        ActivateCalls++;
        Activated = true;
    }

    public Task ReleaseAsync()
    {
        ReleaseCalls++;
        Activated = false;
        Prepared = false;
        return Task.CompletedTask;
    }
}
