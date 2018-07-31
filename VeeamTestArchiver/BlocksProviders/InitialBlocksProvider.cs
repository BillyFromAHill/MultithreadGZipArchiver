﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class InitialBlocksProvider : IBlocksProvider
    {
        private Stream _inputStream;
        private int _bufferSize;
        private int _currentReadBlock = -1;
        private long _bytesProvided = 0;
        private Object _readLock = new Object();

        public InitialBlocksProvider(Stream inputStream, int internalBufferSize = 1024 * 1024)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream");
            }

            _bufferSize = internalBufferSize;
            _inputStream = inputStream;
        }

        public CompressionBlock GetNextBlock()
        {
            var block = new byte[_bufferSize];
            int count = 0;
            lock (_readLock)
            {
                count = _inputStream.Read(block, 0, _bufferSize);
                if (count > 0)
                {
                    _currentReadBlock++;
                    _bytesProvided += count;
                    return new CompressionBlock(_currentReadBlock, block, count);
                }
            }

            return null;
        }

        public long TotalBytes
        {
            get
            {
                // Не для всех потоков вернет то, что нужно, но цели академические.
                return _inputStream.Length;
            }
        }

        public long BytesProvided
        {
            get
            {
                return _bytesProvided;
            }
        }
    }
}
