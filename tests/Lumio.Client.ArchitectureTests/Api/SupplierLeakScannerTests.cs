using System.Net.Sockets;

namespace Lumio.Client.ArchitectureTests.Api;

/// <summary>
/// 遏制测试的反向证明：每个签名维度各留一个必然泄漏的样本类型，钉住
/// <see cref="SupplierLeakScanner"/> 在该维度真会红——闸门不是结构上不可能失败的空转。
/// 样本类型只存在于测试程序集，不进入被扫描的稳定端口程序集。
/// </summary>
public sealed class SupplierLeakScannerTests
{
    [Theory]
    [InlineData(typeof(CtorParameterLeak), "构造函数参数", "System.Net.Sockets.Socket")]
    [InlineData(typeof(PublicFieldLeak), "公开字段", "System.Net.Sockets.Socket")]
    [InlineData(typeof(EventLeak), "事件", "System.Net.Sockets.Socket")]
    [InlineData(typeof(BaseTypeLeakStream), "基类型", "System.IO.Stream")]
    [InlineData(typeof(ExplicitInterfaceLeak), "实现接口", "System.Net.Sockets.Socket")]
    public void EachSignaturePositionIsReported(Type leaking, string position, string offender)
    {
        var leaks = SupplierLeakScanner.Scan(leaking);

        Assert.Contains(
            leaks,
            leak => leak.Contains(position, StringComparison.Ordinal)
                && leak.Contains(offender, StringComparison.Ordinal));
    }

    [Fact]
    public void CleanTypeIsNotReported()
    {
        Assert.Empty(SupplierLeakScanner.Scan(typeof(CleanShape)));
    }

    internal sealed class CtorParameterLeak
    {
        public CtorParameterLeak(Socket transport) => Transport = transport;

        internal Socket Transport { get; }
    }

    internal sealed class PublicFieldLeak
    {
        public static readonly IReadOnlyList<Socket> Transports = [];
    }

    internal sealed class EventLeak
    {
        public event EventHandler<Socket>? Connected;

        internal void Raise(Socket transport) => Connected?.Invoke(this, transport);
    }

    internal abstract class BaseTypeLeakStream : Stream
    {
    }

    // 显式实现：接口方法不是 public 成员，只能从「实现接口」这一维度看见。
    internal sealed class ExplicitInterfaceLeak : IEquatable<Socket>
    {
        bool IEquatable<Socket>.Equals(Socket? other) => false;
    }

    internal sealed class CleanShape
    {
        public CleanShape(int value) => Value = value;

        public int Value { get; }
    }
}
