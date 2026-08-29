using System;
using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Contract;

/// <summary>
/// T-00003 强制项 ③：Endpoint 格式校验 + URI 脱敏。
/// T-00002 交付时既没有东西阻止调用方把凭据塞进 query，`ToString()` 又会把 URI 整串打出去；
/// 当时的脱敏用例 URI 不带 query，抓不到这条。这里补上两侧。
/// </summary>
public sealed class ClientEndpointValidationTests
{
    private static readonly byte[] Credential = { 0xDE, 0xAD, 0xBE, 0xEF, 0x11, 0x22 };
    private static readonly byte[] Nonce = { 0xFE, 0xED, 0xFA, 0xCE };

    private static ClientEndpoint Endpoint(string uri)
    {
        return new ClientEndpoint(uri, Credential, Nonce, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("ws://127.0.0.1:8080/session")]
    [InlineData("wss://host.example:443/session")]
    [InlineData("wss://host.example/session")]
    public void WellFormedWsAndWssAreAccepted(string uri)
    {
        Assert.True(Endpoint(uri).TryValidate(out string reason), reason);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("not-a-uri", "absolute")]
    [InlineData("/session", "absolute")]
    [InlineData("http://host/session", "scheme")]
    [InlineData("https://host/session", "scheme")]
    [InlineData("file:///tmp/x", "scheme")]
    public void MalformedOrWrongSchemeIsRejected(string uri, string expectedReasonFragment)
    {
        Assert.False(Endpoint(uri).TryValidate(out string reason));
        Assert.Contains(expectedReasonFragment, reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("wss://host/session?token=leak")]
    [InlineData("ws://host/session?a=1&b=2")]
    public void QueryIsRejectedBecauseCredentialsRideTheSubProtocol(string uri)
    {
        // 凭据只经 Sec-WebSocket-Protocol 三段位序携带；URI query 不是合法承载位，直接拒。
        Assert.False(Endpoint(uri).TryValidate(out string reason));
        Assert.Contains("query", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserInfoAndFragmentAreRejected()
    {
        Assert.False(Endpoint("wss://user:pass@host/session").TryValidate(out string userInfoReason));
        Assert.Contains("userinfo", userInfoReason, StringComparison.OrdinalIgnoreCase);

        Assert.False(Endpoint("ws://host/session#frag").TryValidate(out string fragmentReason));
        Assert.Contains("fragment", fragmentReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonPositiveConnectTimeoutIsRejected()
    {
        var zero = new ClientEndpoint("ws://host/session", Credential, Nonce, TimeSpan.Zero);
        Assert.False(zero.TryValidate(out string reason));
        Assert.Contains("timeout", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultEndpointIsNotValid()
    {
        Assert.False(default(ClientEndpoint).TryValidate(out _));
    }

    // ---------- 脱敏 ----------

    [Fact]
    public void ToStringRedactsQueryEvenWhenCallerSmuggledCredentialsThere()
    {
        var endpoint = Endpoint("wss://host:443/session?token=SUPERSECRET&sig=abc");
        string rendered = endpoint.ToString();

        Assert.DoesNotContain("SUPERSECRET", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("sig=", rendered, StringComparison.Ordinal);
        // 但仍要看得出连的是哪个端点，否则诊断价值为零。
        Assert.Contains("wss://host:443/session", rendered, StringComparison.Ordinal);
        Assert.Contains("redacted", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToStringRedactsUserInfoAndFragment()
    {
        string withUserInfo = Endpoint("wss://alice:hunter2@host:443/session").ToString();
        Assert.DoesNotContain("hunter2", withUserInfo, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", withUserInfo, StringComparison.Ordinal);
        Assert.Contains("host:443/session", withUserInfo, StringComparison.Ordinal);

        string withFragment = Endpoint("ws://host/session#SUPERSECRET").ToString();
        Assert.DoesNotContain("SUPERSECRET", withFragment, StringComparison.Ordinal);
        Assert.Contains("ws://host/session", withFragment, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRequestRenderingInheritsTheSameRedaction()
    {
        var request = new ClientConnectionCreateRequest(
            3, 32, 16, Endpoint("wss://host:443/session?token=SUPERSECRET"));

        Assert.DoesNotContain("SUPERSECRET", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnparsableUriStillRendersWithoutLeakingRawText()
    {
        // 解析不出来的字符串一律不整串回显：无法证明它不含凭据。
        string rendered = Endpoint("wss://host/session?token=SUPERSECRET but broken").ToString();
        Assert.DoesNotContain("SUPERSECRET", rendered, StringComparison.Ordinal);
    }
}
