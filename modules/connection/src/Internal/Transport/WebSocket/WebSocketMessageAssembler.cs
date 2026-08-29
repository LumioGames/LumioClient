using System;
using System.Collections.Generic;

namespace Lumio.Client.Connection
{
    /// <summary>
    /// 把一条 WS 消息的多次 <c>ReceiveAsync</c> 结果攒成一帧,并在**分配之前**执行长度上限。
    /// </summary>
    /// <remarks>
    /// 攒的是**定长分片列表**,不是一个按对端长度增长的大数组:每次只 <c>new byte[count]</c>,
    /// 而 <c>count</c> 上界是固定接收缓冲。越限时立刻返回 false,调用方中止读取并关闭连接——
    /// 攻击者声称的长度永远不会变成一次分配。物化成连续字节只发生在 <see cref="Complete"/>,
    /// 那时长度已知且不超过上限。
    ///
    /// 本类**不做任何分片重组**:它只处理「一条 WS 消息内部的多次 ReceiveAsync」,
    /// 跨 WS 消息的拼接与拆分一律不实现(架构源 ABS-WIRE-FRAGMENTATION)。
    /// </remarks>
    internal sealed class WebSocketMessageAssembler
    {
        private readonly int _maxMessageBytes;
        private readonly List<byte[]> _chunks = new List<byte[]>();
        private int _total;
        private int _largestAllocationBytes;

        public WebSocketMessageAssembler(int maxMessageBytes)
        {
            _maxMessageBytes = maxMessageBytes;
        }

        /// <summary>本实例存活期间做过的最大单次分配。</summary>
        public int LargestAllocationBytes
        {
            get { return _largestAllocationBytes; }
        }

        public int TotalBytes
        {
            get { return _total; }
        }

        /// <summary>越过上限返回 false,且此时**没有为这次追加分配任何内存**。</summary>
        public bool TryAppend(byte[] source, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (_total > _maxMessageBytes - count)
            {
                return false;
            }

            byte[] chunk = new byte[count];
            Buffer.BlockCopy(source, 0, chunk, 0, count);
            if (count > _largestAllocationBytes)
            {
                _largestAllocationBytes = count;
            }

            _chunks.Add(chunk);
            _total += count;
            return true;
        }

        public byte[] Complete()
        {
            byte[] complete = new byte[_total];
            if (_total > _largestAllocationBytes)
            {
                _largestAllocationBytes = _total;
            }

            int offset = 0;
            for (int i = 0; i < _chunks.Count; i++)
            {
                byte[] chunk = _chunks[i];
                Buffer.BlockCopy(chunk, 0, complete, offset, chunk.Length);
                offset += chunk.Length;
            }

            Reset();
            return complete;
        }

        public void Reset()
        {
            _chunks.Clear();
            _total = 0;
        }
    }
}
