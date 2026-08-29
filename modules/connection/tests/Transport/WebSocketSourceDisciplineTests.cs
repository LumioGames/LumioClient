using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Transport;

/// <summary>
/// 源常量扫描 + 反射：凭据不入源码、WebSocket 类型不穿模块边界、退场纪律注释在场。
/// </summary>
public sealed class WebSocketSourceDisciplineTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "global.json"))
                    && Directory.Exists(Path.Combine(dir.FullName, "modules")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("LumioClient repo root not found.");
        }
    }

    private static string WebSocketSourceDirectory =>
        Path.Combine(RepoRoot, "modules", "connection", "src", "Internal", "Transport", "WebSocket");

    [Fact]
    public void SubProtocolConstantCarriesTheRetirementDiscipline()
    {
        string source = File.ReadAllText(Path.Combine(WebSocketSourceDirectory, "MvpChannelAuth.cs"));

        Assert.Contains("\"lumio.mvp.v0\"", source, StringComparison.Ordinal);
        // 卡面强制：常量处必须写明「双端私有约定 / 不是公共契约 / D-011 冻结后删除 / mvp 与 v0 是退场标记」。
        Assert.Contains("不是公共契约", source, StringComparison.Ordinal);
        Assert.Contains("D-011", source, StringComparison.Ordinal);
        Assert.Contains("退场标记", source, StringComparison.Ordinal);
        Assert.Contains("LumioServer", source, StringComparison.Ordinal);

        // 退场标记不得被摘掉。
        Assert.Equal("lumio.mvp.v0", MvpChannelAuth.SubProtocol);
        Assert.Contains("mvp", MvpChannelAuth.SubProtocol, StringComparison.Ordinal);
        Assert.Contains("v0", MvpChannelAuth.SubProtocol, StringComparison.Ordinal);
    }

    [Fact]
    public void NoWebSocketSourceFileBakesInACredential()
    {
        foreach (string file in Directory.GetFiles(WebSocketSourceDirectory, "*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (string banned in new[] { "token=", "password", "Bearer ", "secret=" })
            {
                Assert.False(
                    source.Contains(banned, StringComparison.OrdinalIgnoreCase),
                    Path.GetFileName(file) + " 出现疑似凭据字面量：" + banned);
            }
        }
    }

    [Fact]
    public void NoWebSocketTypeCrossesTheModulePublicSurface()
    {
        // 与 tests/Lumio.Client.ArchitectureTests 的 PublicApiSupplierLeakTests 同口径，
        // 但在模块内再守一道：System.Net.WebSockets.* 不得出现在本模块公共签名上。
        Assembly assembly = typeof(WebSocketClientConnectionFactory).Assembly;
        foreach (Type type in assembly.GetExportedTypes())
        {
            AssertNotWebSocketType(type);
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                AssertNotWebSocketType(method.ReturnType);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AssertNotWebSocketType(parameter.ParameterType);
                }
            }

            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                AssertNotWebSocketType(property.PropertyType);
            }
        }
    }

    [Fact]
    public void FactoryIsTheOnlyPublicWebSocketEntryPoint()
    {
        Assembly assembly = typeof(WebSocketClientConnectionFactory).Assembly;
        string[] exported = assembly.GetExportedTypes()
            .Where(t => t.Name.Contains("WebSocket", StringComparison.Ordinal))
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Lumio.Client.Connection.WebSocketClientConnectionFactory",
                "Lumio.Client.Connection.WebSocketTransportOptions"
            },
            exported);
    }

    private static void AssertNotWebSocketType(Type type)
    {
        Type inspect = type.IsByRef ? type.GetElementType() ?? type : type;
        if (inspect.IsGenericType)
        {
            foreach (Type argument in inspect.GetGenericArguments())
            {
                AssertNotWebSocketType(argument);
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        string fullName = inspect.FullName ?? inspect.Name;
        Assert.False(
            fullName.StartsWith("System.Net.WebSockets", StringComparison.Ordinal),
            fullName + " 穿过了 connection 模块的公共边界");
        Assert.False(
            fullName.StartsWith("System.Net.Sockets", StringComparison.Ordinal),
            fullName + " 穿过了 connection 模块的公共边界");
    }
}
