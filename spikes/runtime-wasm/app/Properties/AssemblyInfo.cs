// 与 dotnet new wasmbrowser 模板一致：声明本程序集只在 browser 平台运行，否则 CA1416 会把 [JSExport] / [JSMarshalAs] 标成「所有平台可达」。
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]
