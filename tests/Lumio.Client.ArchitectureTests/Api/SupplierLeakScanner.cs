using System.Reflection;

namespace Lumio.Client.ArchitectureTests.Api;

/// <summary>
/// 扫描一个导出类型的全部公共签名位置，逐条报出触碰供应商前缀的泄漏点。
/// 扫描与断言分离，是为了让「闸门自身能不能红」可被 SupplierLeakScannerTests 直接证明。
/// </summary>
internal static class SupplierLeakScanner
{
    internal static readonly string[] BannedPrefixes =
    {
        "UnityEngine",
        "Unity.InputSystem",
        "HybridCLR",
        "Serilog",
        "OpenTelemetry",
        "System.Net.Sockets",
        "System.Net.Security",
        "System.IO.Pipelines",
        "System.IO.Stream",
        "System.Diagnostics.Activity",
        "System.Diagnostics.Metrics",
        "System.Threading.Channels"
    };

    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    internal static IReadOnlyList<string> Scan(Type type)
    {
        var leaks = new List<string>();
        var owner = type.FullName ?? type.Name;

        Collect(leaks, owner, "类型名", type);

        if (type.BaseType is { } baseType)
        {
            Collect(leaks, owner, "基类型", baseType);
        }

        foreach (var contract in type.GetInterfaces())
        {
            Collect(leaks, owner, "实现接口", contract);
        }

        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                Collect(leaks, owner, $"构造函数参数 {parameter.Name}", parameter.ParameterType);
            }
        }

        foreach (var field in type.GetFields(DeclaredPublic))
        {
            Collect(leaks, owner, $"公开字段 {field.Name}", field.FieldType);
        }

        foreach (var declared in type.GetEvents(DeclaredPublic))
        {
            if (declared.EventHandlerType is { } handler)
            {
                Collect(leaks, owner, $"事件 {declared.Name}", handler);
            }
        }

        foreach (var method in type.GetMethods(DeclaredPublic))
        {
            Collect(leaks, owner, $"方法 {method.Name} 返回类型", method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                Collect(leaks, owner, $"方法 {method.Name} 参数 {parameter.Name}", parameter.ParameterType);
            }
        }

        return leaks;
    }

    private static void Collect(List<string> leaks, string owner, string position, Type type)
    {
        foreach (var offender in Offenders(type))
        {
            leaks.Add($"{owner}: {position} 暴露 {offender}");
        }
    }

    private static IEnumerable<string> Offenders(Type type)
    {
        var inspect = type.HasElementType ? type.GetElementType() ?? type : type;

        if (inspect.IsGenericType)
        {
            foreach (var argument in inspect.GetGenericArguments())
            {
                foreach (var nested in Offenders(argument))
                {
                    yield return nested;
                }
            }

            inspect = inspect.GetGenericTypeDefinition();
        }

        var fullName = inspect.FullName ?? inspect.Name;
        foreach (var prefix in BannedPrefixes)
        {
            if (fullName.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return fullName;
            }
        }
    }
}
