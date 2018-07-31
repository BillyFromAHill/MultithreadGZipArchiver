using System;
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

        private int _bufferSize = 1024;
        private bool _disposedValue;
        private int _currentProcessedBlock = -1;


        private CompressionMode _compressionMode;

        private int _blockCacheSize = Environment.ProcessorCount * 2;

        private Queue<CompressionBlock> _readBlocks = new Queue<CompressionBlock>();
        private Queue<CompressionBlock> _blocksToWrite = new Queue<CompressionBlock>();

        private AutoResetEvent _fullEvent = new AutoResetEvent(false);
        private AutoResetEvent _getDataEvent = new AutoResetEvent(false);


        private Thread _readerThread;
        private Thread[] _compressionThreads;
        private Thread _writerThread;

        private Dictionary<int, CompressionBlock> _blocks = new Dictionary<int, CompressionBlock>();

        private IBlocksProvider _blocksProvider;

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

            _compressionThreads = new Thread[Environment.ProcessorCount];
            for (int i = 0; i < _compressionThreads.Length; i++)
            {
                _compressionThreads[i] = new Thread(new ParameterizedThreadStart(CompressionWorker));
                _compressionThreads[i].Start();
            }

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
                    _getDataEvent.WaitOne();
                    continue;
                }

                if (_compressionMode == CompressionMode.Compress)
                {
                    using (var memoryStream = new MemoryStream(_bufferSize))
                    {
                        using (var gzipStream = new GZipStream(memoryStream, _compressionMode))
                        {
                            gzipStream.Write(currentBlock.Data, 0, currentBlock.EffectiveSize);
                        }

                        WriteBlock(new CompressionBlock(currentBlock.BlockIndex, memoryStream.ToArray()));
                    }
                }
                else
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

                if(block == null)
                {
                    Thread.Sleep(0);
                }
            }
        }

        private void WriteBlock(CompressionBlock block)
        {
            lock(_blocksToWrite)
            {
                _blocksToWrite.Enqueue(block);
            }
        }
    }
}
