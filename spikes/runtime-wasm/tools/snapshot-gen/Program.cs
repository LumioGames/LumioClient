// CL-1 探针：生成 world-{N}.lwm1 快照（服务器注册表 + N 个 PlayerEntity），并打印大小与 SHA-256。
// 用法：dotnet run --project tools/snapshot-gen -- <outDir> 100 300 1000
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Samples.Username;
using Lumio.GameRuntime.Samples.Username.Components.Identity;
using Lumio.GameRuntime.Samples.Username.EntityTypes;

namespace Lumio.Client.Spike.RuntimeWasm.SnapshotGen;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: SnapshotGen <outDir> <entityCount> [<entityCount> ...]");
            return 2;
        }

        string outDir = args[0];
        Directory.CreateDirectory(outDir);
        EcsRegistry.Current = GeneratedRegistry.Instance;
        for (int a = 1; a < args.Length; a++)
        {
            int count = int.Parse(args[a], CultureInfo.InvariantCulture);
            using WorldManager manager = WorldManager.Create(GeneratedRegistry.Instance, instanceId: 0x1000000000000001UL);
            manager.Start(Thread.CurrentThread);
            for (int i = 0; i < count; i++)
            {
                EntityOrder order = manager.World.Commands.Create<PlayerEntity>();
                IdentityComponent identity = order.Get<IdentityComponent>();
                identity.AccountId = "acct-" + i.ToString("D4", CultureInfo.InvariantCulture);
                identity.Name.Value = "player-" + i.ToString("D4", CultureInfo.InvariantCulture);
            }

            manager.Tick();
            byte[] bytes = manager.CaptureSnapshot();
            string path = Path.Combine(outDir, "world-" + count.ToString(CultureInfo.InvariantCulture) + ".lwm1");
            File.WriteAllBytes(path, bytes);
            int live = 0;
            foreach (NetEntityId id in manager.World.IssuedIds) if (manager.World.IsLive(id)) live++;
            Console.WriteLine("SNAPSHOT {\"path\":\"" + path.Replace("\\", "/", StringComparison.Ordinal) + "\",\"players\":" + count +
                              ",\"liveEntities\":" + live + ",\"bytes\":" + bytes.Length +
                              ",\"sha256\":\"" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + "\",\"tick\":" + manager.World.Tick + "}");
        }

        return 0;
    }
}
