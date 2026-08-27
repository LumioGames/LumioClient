using System.Reflection;
using Lumio.Client.Session;

namespace Lumio.Client.Session.Tests.Architecture;

public sealed class SessionPublicApiTests
{
    [Fact]
    public void NoUnityHybridClrGameImplementationTypes()
    {
        foreach (var type in typeof(IClientSession).Assembly.GetExportedTypes())
        {
            string name = type.FullName ?? type.Name;
            Assert.DoesNotContain("UnityEngine", name, StringComparison.Ordinal);
            Assert.DoesNotContain("HybridCLR", name, StringComparison.Ordinal);
            Assert.DoesNotContain("LumioGame.ClientGameplay", name, StringComparison.Ordinal);
            Assert.NotEqual("Envelope", type.Name);
        }
    }
}
