namespace Lumio.Client.Bot.Host;

internal static class Program
{
    private static Task<int> Main(string[] args)
    {
        return FoundationHostCommand.RunAsync(args, CancellationToken.None);
    }
}
