using System;
using System.Linq;
using System.Reflection;
using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Contract;

public sealed class ClientEndpointTests
{
    private static readonly byte[] Credential = { 0xDE, 0xAD, 0xBE, 0xEF, 0x11, 0x22 };
    private static readonly byte[] Nonce = { 0xFE, 0xED, 0xFA, 0xCE };

    [Fact]
    public void EndpointCarriesOpaqueCredentialNonceAndTimeout()
    {
        var endpoint = new ClientEndpoint("wss://host:443/session", Credential, Nonce, TimeSpan.FromSeconds(7));

        Assert.True(endpoint.IsConfigured);
        Assert.Equal("wss://host:443/session", endpoint.Uri);
        Assert.Equal(Credential, endpoint.Credential.ToArray());
        Assert.Equal(Nonce, endpoint.Nonce.ToArray());
        Assert.Equal(TimeSpan.FromSeconds(7), endpoint.ConnectTimeout);
    }

    [Fact]
    public void DefaultEndpointIsNotConfigured()
    {
        // LocalEmbedded 路径不带 endpoint，必须能与「已配置远程」区分开。
        Assert.False(default(ClientEndpoint).IsConfigured);
    }

    [Fact]
    public void ToStringRedactsCredentialAndNonce()
    {
        var endpoint = new ClientEndpoint("wss://host:443/session", Credential, Nonce, TimeSpan.FromSeconds(7));
        var request = new ClientConnectionCreateRequest(3, 32, 16, endpoint);

        foreach (var rendered in new[] { endpoint.ToString(), request.ToString() })
        {
            Assert.NotNull(rendered);
            AssertNoSecretSpelling(rendered!, Credential);
            AssertNoSecretSpelling(rendered!, Nonce);
        }

        // 但必须仍然能看出「带了凭据」，否则诊断价值为零。
        Assert.Contains("wss://host:443/session", endpoint.ToString(), StringComparison.Ordinal);
    }

    internal static void AssertNoSecretSpelling(string rendered, byte[] secret)
    {
        foreach (var spelling in Spellings(secret))
        {
            Assert.DoesNotContain(spelling, rendered, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static string[] Spellings(byte[] secret)
    {
        return new[]
        {
            Convert.ToBase64String(secret),
            string.Concat(secret.Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))),
            string.Join(", ", secret.Select(b => b.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        };
    }
}

public sealed class CredentialContainmentTests
{
    private static readonly byte[] Credential = { 0xDE, 0xAD, 0xBE, 0xEF, 0x11, 0x22 };
    private static readonly byte[] Nonce = { 0xFE, 0xED, 0xFA, 0xCE };

    [Fact]
    public void EventAndSnapshotTypesDeclareNoCredentialCarryingMember()
    {
        foreach (var type in new[] { typeof(ConnectionEvent), typeof(ClientConnectionSnapshot), typeof(EncodedFrame) })
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Select(m => m.Name))
            {
                foreach (var forbidden in new[] { "Credential", "Nonce", "Token", "Secret" })
                {
                    Assert.False(
                        member.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                        type.Name + " 不得声明承载凭据的成员：" + member);
                }
            }
        }
    }

    // 【本测试当前是绊线，不是遏制证明 —— 读之前先看清它证明了什么】
    //
    // T-00002 只扩公共面、不实现任何传输逻辑，`ClientConnectionFactory.Create` 因此
    // 不消费 request.Endpoint：凭据字节根本没有进入连接的对象图。所以下面这几条断言
    // 在**当前实现下不可能失败**，它们证明不了「凭据被正确遏制」。
    //
    // 真正带电的遏制断言只有 ClientEndpointTests.ToStringRedactsCredentialAndNonce
    // （把 Credential.Length 换成字节内容会立刻变红）。
    //
    // 保留本测试的意义是：T-00003 把 endpoint 接进 WSS 传输、凭据开始真实流经这条
    // 链路的那一刻，它会自动变成有效断言。**T-00003 必须重新核验这组断言确实会红**
    // （例如故意把凭据拼进出站帧，确认本测试失败），否则它会退化成虚假安全感。
    [Fact]
    public void CredentialsDoNotReachDrainedEventsOrSnapshot_TripwireForRemoteTransport()
    {
        var endpoint = new ClientEndpoint("wss://host:443/session", Credential, Nonce, TimeSpan.FromSeconds(5));
        var factory = new ClientConnectionFactory();
        var created = factory.Create(new ClientConnectionCreateRequest(1, 8, 4, endpoint), out var connection);
        Assert.True(created.Succeeded);

        connection.Start();
        Assert.True(created.TryGetLoopback(out var loopback));
        Assert.True(loopback.TryDeliverToClient(new EncodedFrame(new byte[] { 1, 2, 3 })));

        var buffer = new ConnectionEvent[8];
        int n = connection.DrainEvents(buffer);
        Assert.True(n > 0);

        for (int i = 0; i < n; i++)
        {
            var bytes = buffer[i].Frame.Bytes.ToArray();
            Assert.False(ContainsSequence(bytes, Credential), "凭据字节泄漏进 ConnectionEvent.Frame");
            Assert.False(ContainsSequence(bytes, Nonce), "nonce 字节泄漏进 ConnectionEvent.Frame");
            ClientEndpointTests.AssertNoSecretSpelling(buffer[i].ToString() ?? string.Empty, Credential);
        }

        ClientEndpointTests.AssertNoSecretSpelling(connection.GetSnapshot().ToString() ?? string.Empty, Credential);
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class LoopbackAvailabilityTests
{
    [Fact]
    public void RemoteResultReportsNoLoopbackAndDoesNotThrow()
    {
        // 远程工厂返回的结果没有 Loopback。调用方必须有判空口，且读它本身不得抛。
        var remote = new ClientConnectionCreateResult(true);

        Assert.False(remote.HasLoopback);
        Assert.False(remote.TryGetLoopback(out _));

        var exception = Record.Exception(() => remote.Loopback);
        Assert.Null(exception);
    }

    [Fact]
    public void LocalEmbeddedResultExposesLoopback()
    {
        var factory = new ClientConnectionFactory();
        var created = factory.Create(new ClientConnectionCreateRequest(1, 8), out _);

        Assert.True(created.HasLoopback);
        Assert.True(created.TryGetLoopback(out var loopback));
        Assert.NotNull(loopback);
    }
}

public sealed class FaultPolicyInjectionTests
{
    private sealed class CountingFaultPolicy : ITransportFaultPolicy
    {
        public int Calls { get; private set; }

        public TransportFaultAction Decide(in TransportFaultContext context)
        {
            _ = context;
            Calls++;
            return TransportFaultAction.Pass;
        }
    }

    [Fact]
    public void InjectedPolicyIsActuallyConsulted()
    {
        var policy = new CountingFaultPolicy();
        var factory = new ClientConnectionFactory(policy);
        factory.Create(new ClientConnectionCreateRequest(1, 1), out var connection);
        connection.Start();

        // 填满底层传输，迫使发送落进 send queue；下一次发送时 flush 会咨询 policy。
        var frame = new EncodedFrame(new byte[] { 7 });
        var accepted = new bool[12];
        for (int i = 0; i < accepted.Length; i++)
        {
            accepted[i] = connection.TrySend(in frame).Accepted;
        }

        // 先自证前提：若容量常量变化导致没有溢出，本测试的失败原因必须一眼可读。
        Assert.Contains(false, accepted);
        Assert.True(policy.Calls > 0, "注入的 ITransportFaultPolicy.Decide 从未被调用");
    }

    [Fact]
    public void DefaultFactoryStillUsesPassThrough()
    {
        var factory = new ClientConnectionFactory();
        var created = factory.Create(new ClientConnectionCreateRequest(1, 8), out var connection);

        Assert.True(created.Succeeded);
        Assert.True(connection.Start().Succeeded);
    }
}

public sealed class QueueBudgetTests
{
    [Fact]
    public void DefaultsMatchCurrentBehaviour()
    {
        Assert.Equal(32, ClientConnectionCreateRequest.DefaultEventCapacity);
        Assert.Equal(16, ClientConnectionCreateRequest.DefaultDrainLimit);

        // 既有两参构造必须保持原语义，并补上 drain 默认值。
        var legacy = new ClientConnectionCreateRequest(5, 32);
        Assert.Equal(32, legacy.EventCapacity);
        Assert.Equal(16, legacy.DrainLimit);
        Assert.False(legacy.Endpoint.IsConfigured);
    }

    [Fact]
    public void CapacityAndDrainLimitAreConfigurable()
    {
        var request = new ClientConnectionCreateRequest(5, 64, 8, default);
        Assert.Equal(64, request.EventCapacity);
        Assert.Equal(8, request.DrainLimit);
    }

}
