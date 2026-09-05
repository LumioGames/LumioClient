// CL-1 探针：桌面对照。用法：dotnet run -c Release --project tools/desktop-bench -- <fixturesDir> <inputs> <repeats>
// 输出每个快照的 repeats 次样本（JSON 行）+ 中位数 / 最差 + 哈希；哈希与浏览器输出逐位比对。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Lumio.Client.Spike.RuntimeWasm.DesktopBench;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: DesktopBench <fixturesDir> <inputs> <repeats>");
            return 2;
        }

        string dir = args[0];
        int inputs = int.Parse(args[1], CultureInfo.InvariantCulture);
        int repeats = int.Parse(args[2], CultureInfo.InvariantCulture);
        Console.WriteLine("PROBE " + RuntimeProbe.Describe());
        foreach (string path in Directory.GetFiles(dir, "world-*.lwm1").OrderBy(p => new FileInfo(p).Length))
        {
            byte[] bytes = File.ReadAllBytes(path);
            var samples = new List<RebuildSample>();
            for (int i = 0; i < repeats; i++) samples.Add(RebuildBench.Run(bytes, inputs));
            double[] totals = samples.Select(s => s.TotalMs).OrderBy(v => v).ToArray();
            double[] creates = samples.Select(s => s.CreateMs).OrderBy(v => v).ToArray();
            double[] applies = samples.Select(s => s.ApplyMs).OrderBy(v => v).ToArray();
            string hashes = string.Join(",", samples.Select(s => s.Hash.ToString("x16", CultureInfo.InvariantCulture)).Distinct());
            Console.WriteLine("REBUILD {\"file\":\"" + Path.GetFileName(path) + "\",\"bytes\":" + bytes.Length +
                              ",\"livePlayers\":" + samples[0].LivePlayers + ",\"inputs\":" + inputs + ",\"repeats\":" + repeats +
                              ",\"createMsMedian\":" + F(Median(creates)) + ",\"createMsWorst\":" + F(creates[^1]) +
                              ",\"applyMsMedian\":" + F(Median(applies)) + ",\"applyMsWorst\":" + F(applies[^1]) +
                              ",\"totalMsMedian\":" + F(Median(totals)) + ",\"totalMsWorst\":" + F(totals[^1]) + ",\"totalMsBest\":" + F(totals[0]) +
                              ",\"hash\":\"" + hashes + "\",\"samplesMs\":[" + string.Join(",", samples.Select(s => F(s.TotalMs))) + "]}");
        }

        return 0;
    }

    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2.0;
}
