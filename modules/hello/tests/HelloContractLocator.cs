namespace Lumio.Client.Hello.Tests;

/// <summary>
/// 定位架构仓的 hello-wire-v1.json 真身:优先 LUMIO_HELLO_WIRE_CONTRACT 环境变量,
/// 否则从测试程序集向上找兄弟 LumioGameEngineArchitecture 检出。找不到返回 null(用例按
/// Skip 语义跳过并输出说明)——本仓不内嵌契约副本。
/// </summary>
internal static class HelloContractLocator
{
    public const string EnvironmentVariable = "LUMIO_HELLO_WIRE_CONTRACT";

    public static string? Locate()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return File.Exists(fromEnvironment) ? fromEnvironment : null;
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "LumioGameEngineArchitecture",
                "engine",
                "wire",
                "hello-wire-v1.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
