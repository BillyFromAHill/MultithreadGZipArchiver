using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace VeeamTestArchiver
{
    public class MultiThreadGZipStream : IDisposable
    {
        private Stream _inputStream;

        private int _bufferSize;
        private bool _disposedValue;
        private int _currentProcessedBlock = -1;
        private int _currentReadBlock = -1; 

        private CompressionMode _compressionMode;

        private Object _readLock = new Object();
        private Thread[] _threads;
        private Dictionary<int, CompressionBlock> _blocks = new Dictionary<int, CompressionBlock>();

        private CompressedBlocksProvider _compressedProvider;

        public MultiThreadGZipStream(
            Stream inputStream,
            CompressionMode compressionMode,
            int bufferSize)
        {
            if (inputStream == null)
            {
                throw new ArgumentNullException("inputStream");
            }

            _bufferSize = bufferSize;
            _inputStream = inputStream;
            _compressionMode = compressionMode;

            _compressedProvider = new CompressedBlocksProvider(_inputStream);
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

        private void CompressionWorker(Object stream)
        {
            while (true)
            {
                CompressionBlock currentBlock = ReadBlock();

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

        private CompressionBlock ReadBlock()
        {
            if (_compressionMode == CompressionMode.Compress)
            {
                var block = new byte[_bufferSize];
                int count = 0;
                lock (_readLock)
                {
                    count = _inputStream.Read(block, 0, _bufferSize);
                    if (count > 0)
                    {
                        _currentReadBlock++;
                        return new CompressionBlock(_currentReadBlock, block, count);
                    }
                }
            }
            else
            {
                lock (_readLock)
                {
                    return _compressedProvider.GetNextBlock();
                }
            }

            return null;
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
