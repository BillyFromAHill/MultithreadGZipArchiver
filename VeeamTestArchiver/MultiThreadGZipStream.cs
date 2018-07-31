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

        private Thread[] _threads;
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

            _threads = new Thread[Environment.ProcessorCount];
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(new ParameterizedThreadStart(CompressionWorker));
                _threads[i].Start(destStream);
            }
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
                return _threads.All(t => !t.IsAlive);
            }
        }

        private void CompressionWorker(Object stream)
        {
            while (true)
            {
                CompressionBlock currentBlock = _blocksProvider.GetNextBlock();

                if (currentBlock == null)
                {
                    break;
                }

                if (_compressionMode == CompressionMode.Compress)
                {
                    using (var memoryStream = new MemoryStream(_bufferSize))
                    {
                        using (var gzipStream = new GZipStream(memoryStream, _compressionMode))
                        {
                            gzipStream.Write(currentBlock.Data, 0, currentBlock.EffectiveSize);
                        }

                        WriteBlock(new CompressionBlock(currentBlock.BlockIndex, memoryStream.ToArray()), stream as Stream);
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

                        WriteBlock(new CompressionBlock(currentBlock.BlockIndex, memoryStream.ToArray()), stream as Stream);
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
                    if (_threads != null)
                    {
                        foreach (var thread in _threads)
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

        private void WriteBlock(CompressionBlock block, Stream stream)
        {
            if (block.BlockIndex - _currentProcessedBlock == 1)
            {
                stream.Write(block.Data, 0, block.EffectiveSize);
                _currentProcessedBlock++;
            }
            else
            {
                lock (_blocks)
                {
                    _blocks.Add(block.BlockIndex, block);
                }
            }

            lock (_blocks)
            {
                while (_blocks.ContainsKey(_currentProcessedBlock + 1))
                {
                    CompressionBlock currentBlock = _blocks[_currentProcessedBlock + 1];
                    _blocks.Remove(_currentProcessedBlock);
                    stream.Write(currentBlock.Data, 0, currentBlock.EffectiveSize);
                    _currentProcessedBlock++;
                }
            }
        }
    }
}
