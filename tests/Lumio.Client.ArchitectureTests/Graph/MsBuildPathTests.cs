namespace Lumio.Client.ArchitectureTests.Graph;

// 这些断言在任何宿主上都必须成立。修复前，反斜杠形式在 macOS / Linux 上原样返回整串
// 而不报错，于是三个依赖它的图测试只在 Windows 上会绿。
public sealed class MsBuildPathTests
{
    [Theory]
    [InlineData(@"..\..\session\src\Lumio.Client.Session.csproj")]
    [InlineData("../../session/src/Lumio.Client.Session.csproj")]
    [InlineData("Lumio.Client.Session.csproj")]
    public void ProjectNameIsSeparatorAgnostic(string include)
    {
        Assert.Equal("Lumio.Client.Session", MsBuildPath.ProjectName(include));
    }
}
