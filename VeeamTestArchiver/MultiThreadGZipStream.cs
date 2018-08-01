﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace VeeamTestArchiver
{
    public class MultiThreadGZipStream : IDisposable, IArchiverStatistics
    {
        // Заименовано как Stream,
        // поскольку без особых доработок может реализовывать полноценный поток,
        // что изначально задумывалось.
        private Stream _inputStream;

        private int _bufferSize = 1024 * 1024;
        private bool _disposedValue;
        private int _currentProcessedBlock = -1;

        private CompressionMode _compressionMode;

        private int _blockCacheSize = Environment.ProcessorCount * 2;

        private Queue<CompressionBlock> _readBlocks = new Queue<CompressionBlock>();
        private Queue<CompressionBlock> _blocksToWrite = new Queue<CompressionBlock>();

        private AutoResetEvent _fullEvent = new AutoResetEvent(false);
        private AutoResetEvent _getDataEvent = new AutoResetEvent(false);
        private AutoResetEvent _writeReadyEvent = new AutoResetEvent(false);

        private Thread _readerThread;
        private List<Thread> _compressionThreads;
        private Thread _writerThread;

        private Dictionary<int, CompressionBlock> _blocks = new Dictionary<int, CompressionBlock>();

        private IBlocksProvider _blocksProvider;

        private bool _parallelDecompression = false;

        private GZipStream _decompressionStream = null;
        private MemoryStream _memoryToDecompress = null;

        public MultiThreadGZipStream(
            Stream inputStream,
            CompressionMode compressionMode)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream");
            }

            _inputStream = inputStream;
            _compressionMode = compressionMode;

            if (compressionMode == CompressionMode.Compress)
            {
                _blocksProvider = new InitialBlocksProvider(_inputStream);
            }
            else
            {
                _blocksProvider = new CompressedBlocksProvider(_inputStream);
            }
        }

        public void CopyTo(Stream destStream)
        {
            _currentProcessedBlock = -1;

            _readerThread = new Thread(new ParameterizedThreadStart(ReaderWorker));
            _readerThread.Start(_inputStream);

            _compressionThreads = new List<Thread>();

            // Первый поток запустит больше, в случае необходимости.
            var firstThread = new Thread(new ParameterizedThreadStart(CompressionWorker));
            _compressionThreads.Add(firstThread);
            firstThread.Start();

            _writerThread = new Thread(new ParameterizedThreadStart(WriteWorker));
            _writerThread.Start(destStream);
        }

        public double PercentsDone
        {
            get
            {
                return _blocksProvider.BytesProvided / (double)_blocksProvider.TotalBytes * 100;
            }
        }

        public bool IsDone
        {
            get
            {
                return _compressionThreads.All(t => !t.IsAlive);
            }
        }

        private void ReaderWorker(Object stream)
        {
            CompressionBlock block;
            while ((block = _blocksProvider.GetNextBlock()) != null)
            {
                lock (_readBlocks)
                {
                    _readBlocks.Enqueue(block);
                    _getDataEvent.Set();
                }

                if (_readBlocks.Count >= _blockCacheSize)
                {
                    _fullEvent.WaitOne();
                }
            }

            _getDataEvent.Set();
        }

        private CompressionBlock ReadBlock()
        {
            if (_readBlocks.Count <= _blockCacheSize / 2)
            {
                _fullEvent.Set();
            }

            lock (_readBlocks)
            {
                if (_readBlocks.Any())
                {
                    return _readBlocks.Dequeue();
                }
            }

            return null;
        }

        private void CompressionWorker(Object stream)
        {
            while (_readerThread.IsAlive || _readBlocks.Count > 0)
            {
                CompressionBlock currentBlock = ReadBlock();

                if (currentBlock == null)
                {
                    if (_readerThread.IsAlive)
                    {
                        _getDataEvent.WaitOne(500);
                    }

                    continue;
                }

                if (currentBlock.BlockIndex == 0)
                {
                    int desiredThreadsCount = 1;

                    _parallelDecompression = false;
                    if (_compressionMode == CompressionMode.Compress )
                    {
                        desiredThreadsCount = Environment.ProcessorCount;
                        _parallelDecompression = true;
                    }
                    else
                    {
                        GZipHeader header = new GZipHeader(currentBlock.Data);
                        byte[] sizeData = header.GetExtra(1, 4);

                        if (sizeData != null && BitConverter.ToInt32(sizeData, 0) > 0)
                        {
                            desiredThreadsCount = Environment.ProcessorCount;
                            _parallelDecompression = true;
                        }
                    }

                    for (int i = 0; i < desiredThreadsCount - 1; i++)
                    {
                        var thread = new Thread(new ParameterizedThreadStart(CompressionWorker));
                        _compressionThreads.Add(thread);
                        thread.Start();
                    }
                }

                if (_compressionMode == CompressionMode.Compress)
                {
                    Compress(currentBlock);
                }
                else
                {
                    Decompress(currentBlock);
                }
            }

            if (_decompressionStream != null)
            {
                _decompressionStream.Dispose();
            }

            if (_memoryToDecompress != null)
            {
                _memoryToDecompress.Dispose();
            }
        }

        private void Decompress(CompressionBlock currentBlock)
        {
            if (_parallelDecompression)
            {
                using (var memoryStream = new MemoryStream(_bufferSize))
                {
                    using (var gzipStream = new GZipStream(
                        new MemoryStream(currentBlock.Data),
                        _compressionMode))
                    {
                        byte[] decompressedData = new byte[_bufferSize];

                        int nRead;
                        while ((nRead = gzipStream.Read(decompressedData, 0, decompressedData.Length)) > 0)
                        {
                            memoryStream.Write(decompressedData, 0, nRead);
                        }
                    }

                    WriteBlock(new CompressionBlock(currentBlock.BlockIndex, memoryStream.ToArray()));
                }
            }
            else
            {
                if (_decompressionStream == null)
                {
                    _memoryToDecompress = new MemoryStream();

                    _decompressionStream = new GZipStream(_memoryToDecompress, _compressionMode);
                }

                _memoryToDecompress.Write(currentBlock.Data, 0, currentBlock.Data.Length);
                _memoryToDecompress.Seek(-currentBlock.Data.Length, SeekOrigin.End);
                using (var memoryStream = new MemoryStream(_bufferSize))
                {
                    byte[] decompressedData = new byte[_bufferSize];

                    int nRead;
                    while ((nRead = _decompressionStream.Read(decompressedData, 0, decompressedData.Length)) > 0)
                    {
                        memoryStream.Write(decompressedData, 0, nRead);
                    }

                    WriteBlock(new CompressionBlock(currentBlock.BlockIndex, memoryStream.ToArray()));
                }
            }
        }

        private void Compress(CompressionBlock block)
        {
            using (var memoryStream = new MemoryStream(_bufferSize))
            {
                using (var gzipStream = new GZipStream(memoryStream, _compressionMode))
                {
                    gzipStream.Write(block.Data, 0, block.EffectiveSize);
                }


                byte[] data = memoryStream.ToArray();
                GZipHeader gZipHeader = new GZipHeader(data);

                int initialHeaderSize = gZipHeader.Header.Length;

                int size = 0;
                // Добавляем поле с размером.
                gZipHeader.SetExtra(1, 4, BitConverter.GetBytes(size));

                // Устанавливаем конечный размер блока.
                size = (int)(gZipHeader.Header.Length + data.Length - initialHeaderSize);

                gZipHeader.SetExtra(1, 4, BitConverter.GetBytes(size));

                byte[] resultBlock = new byte[size];
                Array.Copy(gZipHeader.Header, resultBlock, gZipHeader.Header.Length);
                Array.Copy(data, initialHeaderSize, resultBlock, gZipHeader.Header.Length, data.Length - initialHeaderSize);

                WriteBlock(new CompressionBlock(block.BlockIndex, resultBlock));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_compressionThreads != null)
                    {
                        foreach (var thread in _compressionThreads)
                        {

                        }
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }


        private void WriteWorker (Object streamObject)
        {
            Stream stream = streamObject as Stream;

            while(_blocksToWrite.Count > 0 || _compressionThreads.Any(t => t.IsAlive))
            {
                CompressionBlock block = null;
                lock(_blocksToWrite)
                {
                    if (_blocksToWrite.Any())
                    {
                        block = _blocksToWrite.Dequeue();
                    }
                }

                if (block != null)
                {
                    if (block.BlockIndex - _currentProcessedBlock == 1)
                    {
                        stream.Write(block.Data, 0, block.EffectiveSize);
                        _currentProcessedBlock++;
                    }
                    else
                    {
                        _blocks.Add(block.BlockIndex, block);
                    }
                }

                while (_blocks.ContainsKey(_currentProcessedBlock + 1))
                {
                    CompressionBlock currentBlock = _blocks[_currentProcessedBlock + 1];
                    _blocks.Remove(_currentProcessedBlock);
                    stream.Write(currentBlock.Data, 0, currentBlock.EffectiveSize);
                    _currentProcessedBlock++;
                }

                if (block == null)
                {
                    _writeReadyEvent.WaitOne(500);
                }
            }
        }

        private void WriteBlock(CompressionBlock block)
        {
            lock(_blocksToWrite)
            {
                _blocksToWrite.Enqueue(block);
                _writeReadyEvent.Set();
            }
        }
    }
}
