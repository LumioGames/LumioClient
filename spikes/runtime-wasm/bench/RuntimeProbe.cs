// CL-1 探针：运行环境自描述——注册表 / 生成物 / 嵌入资源 / 线程模型，wasm 与桌面各跑一次对照。
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Samples.Username;
using Lumio.GameRuntime.Samples.Username.EntityTypes;

namespace Lumio.Client.Spike.RuntimeWasm;

public static class RuntimeProbe
{
    public const string AttributeDeclarationsResource = "Lumio.GameRuntime.Ecs.generated.attribute-declarations.json";

    public static string Describe()
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("frameworkDescription", RuntimeInformation.FrameworkDescription);
            writer.WriteString("osDescription", RuntimeInformation.OSDescription);
            writer.WriteString("processArchitecture", RuntimeInformation.ProcessArchitecture.ToString());
            writer.WriteBoolean("isBrowser", OperatingSystem.IsBrowser());
            writer.WriteNumber("processorCount", Environment.ProcessorCount);
            writer.WriteNumber("managedThreadId", Environment.CurrentManagedThreadId);
            writer.WriteNumber("currentThreadManagedId", Thread.CurrentThread.ManagedThreadId);
            writer.WriteBoolean("threadCurrentThreadStable", ReferenceEquals(Thread.CurrentThread, Thread.CurrentThread));
            writer.WriteString("newThreadStart", TryStartThread());
            writer.WriteNumber("interlockedIncrement", InterlockedProbe());
            writer.WriteString("registrySide", GeneratedRegistry.Instance.Side.ToString());
            writer.WriteNumber("attributeDeclarations", GeneratedRegistry.Instance.AttributeDeclarations.Count);
            writer.WriteNumber("playerComponents", GeneratedRegistry.Instance.CreateComponents(typeof(PlayerEntity)).Length);
            writer.WriteString("ecsAssembly", typeof(WorldManager).Assembly.GetName().Name);
            writer.WriteNumber("embeddedDeclarationsBytes", EmbeddedResourceLength());
            writer.WriteString("embeddedResourceNames", string.Join(";", typeof(WorldManager).Assembly.GetManifestResourceNames()));
            writer.WriteBoolean("stopwatchHighResolution", System.Diagnostics.Stopwatch.IsHighResolution);
            writer.WriteNumber("stopwatchFrequency", System.Diagnostics.Stopwatch.Frequency);
            writer.WriteBoolean("serverGc", System.Runtime.GCSettings.IsServerGC);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string GcInfo()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        return "{\"gen0\":" + GC.CollectionCount(0) + ",\"gen1\":" + GC.CollectionCount(1) + ",\"gen2\":" + GC.CollectionCount(2) +
               ",\"totalMemory\":" + GC.GetTotalMemory(false) + ",\"heapSizeBytes\":" + info.HeapSizeBytes +
               ",\"totalCommittedBytes\":" + info.TotalCommittedBytes + "}";
    }

    private static long EmbeddedResourceLength()
    {
        using Stream? stream = typeof(WorldManager).Assembly.GetManifestResourceStream(AttributeDeclarationsResource);
        return stream?.Length ?? -1;
    }

    private static string TryStartThread()
    {
        try
        {
            int seen = 0;
            var thread = new Thread(() => Interlocked.Exchange(ref seen, 1));
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(2));
            return seen == 1 ? "ok" : "started-but-did-not-run";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    private static int InterlockedProbe()
    {
        int value = 0;
        for (int i = 0; i < 1000; i++) Interlocked.Increment(ref value);
        return value;
    }
}
