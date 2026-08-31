using Lumio.Client.Hello;

namespace Lumio.Client.HelloBot;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        return await HelloBotCli.RunAsync(args).ConfigureAwait(false);
    }
}
