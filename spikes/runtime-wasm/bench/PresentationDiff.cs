// CL-1 探针：RT-3 IPresentationDiff 形状的样例产出（Started / Continued / Ended，键 = 实体类型 + fx_key + 稳定参数），
// 只用于测 [JSExport] 每帧把差集交给 JS / Canvas 的开销；不是正式表现层。
using System;
using System.Text;

namespace Lumio.Client.Spike.RuntimeWasm;

public static class PresentationDiff
{
    /// <summary>JSON 形态：每帧 N 个 Continued 键 + 首帧全部 Started。</summary>
    public static string BuildJson(int entities, int frame)
    {
        var sb = new StringBuilder(entities * 48 + 64);
        sb.Append("{\"frame\":").Append(frame).Append(",\"started\":[");
        if (frame == 0)
        {
            for (int i = 0; i < entities; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("\"player:idle:").Append(i).Append('"');
            }
        }

        sb.Append("],\"continued\":[");
        for (int i = 0; i < entities; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"k\":\"player:idle:").Append(i).Append("\",\"x\":").Append(X(i, frame)).Append(",\"y\":").Append(Y(i, frame)).Append('}');
        }

        sb.Append("],\"ended\":[]}");
        return sb.ToString();
    }

    /// <summary>打包形态：每实体 4 个 int（kind, keyHash, x, y），JS 侧按下标读。</summary>
    public static int[] BuildPacked(int entities, int frame)
    {
        int[] data = new int[entities * 4];
        for (int i = 0; i < entities; i++)
        {
            data[i * 4] = frame == 0 ? 1 : 2;
            data[(i * 4) + 1] = i;
            data[(i * 4) + 2] = X(i, frame);
            data[(i * 4) + 3] = Y(i, frame);
        }

        return data;
    }

    private static int X(int i, int frame) => ((i * 37) + frame) % 800;

    private static int Y(int i, int frame) => ((i * 91) + (frame * 2)) % 600;
}
