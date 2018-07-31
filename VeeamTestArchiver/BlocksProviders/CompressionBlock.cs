using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class CompressionBlock
    {
        public CompressionBlock(int blockIndex, byte[] data)
        {
            BlockIndex = blockIndex;

            Data = data;

            EffectiveSize = data.Length;
        }

        public CompressionBlock(int blockIndex, byte[] data, int effectiveSize)
        {
            BlockIndex = blockIndex;

            Data = data;

            EffectiveSize = effectiveSize;
        }

        public int BlockIndex { get; private set; }

        public byte[] Data { get; private set; }

        public int EffectiveSize { get; private set; }
    }
}
