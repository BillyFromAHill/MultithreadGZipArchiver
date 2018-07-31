using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class CompressedBlocksProvider : IBlocksProvider
    {
        // Чтение выделено в объекты, поскольку внутри может быть реализовано чтение файла в несколько потоков,
        // что для RAID, например, может дать прирост скорости.
        // Для чтения сжатых блоков выбран вариант вычитывания до идентификатора gzip.
        // Переупаковка блока, с дополнением в виде размера блока в заголовке, дала бы более быстрое чтение, но
        // проявилось бы это только при чтении упакованного этой же утилитой файла и необходимость
        // реализации вычитывания до следующего заголовка все равно бы не отпала.
        private Stream _gzippedStream;
        private Object _streamLock = new Object();

        private long _bytesProvided = 0;
        private int _currentBlockIndex = -1;

        private  int _internalBufferSize = 1024 * 1024;

        private byte[] _currentInternalBlock = null;
        private int _currentBlockRead = 0;

        // Байты ID используются как разделитель.
        private int _currentInternalBlockPosition = 3;

        private const byte GZipId1 = 0x1f;
        private const byte GZipId2 = 0x8b;
        private const byte GZipDeflate = 0x08;



        public CompressedBlocksProvider(Stream gzippedStream, int internalBufferSize = 1024 * 1024)
        {
            if (gzippedStream == null)
            {
                throw new ArgumentNullException("gzippedStream");
            }

            _internalBufferSize = internalBufferSize;
            _gzippedStream = gzippedStream;
        }

        public CompressionBlock GetNextBlock()
        {
            lock (_streamLock)
            {
                if (_currentInternalBlock == null)
                {
                    _currentInternalBlock = new byte[_internalBufferSize];

                    _currentBlockRead = _gzippedStream.Read(_currentInternalBlock, 0, _internalBufferSize);

                    if (_currentBlockRead == 0)
                    {
                        return null;
                    }

                    if (_currentBlockRead > 2 && (_currentInternalBlock[0] != GZipId1 || _currentInternalBlock[1] != GZipId2))
                    {
                        throw new MissingFieldException("Ids are not found.");
                    }
                }

                _currentBlockIndex++;
                bool firstFound = false;
                bool secondFound = false;
                bool thirdFound = false;


                // Сжатый блок может быть, любого размера, в том числе, больше исходного.
                var blocks = new List<byte[]>();

                int firstBlockStart = _currentInternalBlockPosition;
                int currentBlockStart = _currentInternalBlockPosition;
                int lastBlockRead = _currentBlockRead;
                byte[] currentBlock = _currentInternalBlock;

                do
                {
                    _currentBlockRead = lastBlockRead;
                    _currentInternalBlock = currentBlock;
                    blocks.Add(_currentInternalBlock);
                    for (int i = currentBlockStart; i < _currentBlockRead; i++)
                    {
                        if (secondFound && _currentInternalBlock[i] == GZipDeflate)
                        {
                            thirdFound = true;
                            _currentInternalBlockPosition = i + 1;
                            break;
                        }

                        secondFound = firstFound && _currentInternalBlock[i] == GZipId2;
                        firstFound = _currentInternalBlock[i] == GZipId1;
                    }

                    if (thirdFound)
                    {
                        break;
                    }

                    currentBlockStart = 0;
                    currentBlock = new byte[_internalBufferSize];
                    lastBlockRead = _gzippedStream.Read(currentBlock, 0, _internalBufferSize);
                }
                while (lastBlockRead > 0);

                if (!thirdFound)
                {
                    _currentInternalBlockPosition = _currentBlockRead + 2;
                }

                if (_currentInternalBlockPosition == firstBlockStart)
                {
                    return null;
                }

                long resultBlockSize =
                    (blocks.Count - 1) * _internalBufferSize + _currentInternalBlockPosition - firstBlockStart;

                byte[] resultBuffer = new byte[resultBlockSize];
                int blockIndex = 0;
                long placePosition = 3;
                foreach (var block in blocks)
                {
                    long bytesToCopy = _internalBufferSize;
                    long copyStart = 0;

                    if (blockIndex == 0)
                    {
                        copyStart = firstBlockStart;
                        bytesToCopy = _internalBufferSize - copyStart;
                    }

                    // Начало сжатого куска в одном внутреннем блоке, конец - в другом.
                    if (blockIndex == blocks.Count - 1)
                    {
                        if (blocks.Count == 1)
                        {
                            bytesToCopy = _currentInternalBlockPosition - 3 - firstBlockStart;
                        }
                        else
                        {
                            bytesToCopy = _currentInternalBlockPosition - 3;
                        }
                    }

                    Array.Copy(
                        block,
                        copyStart,
                        resultBuffer,
                        placePosition,
                        bytesToCopy);

                    placePosition += bytesToCopy;

                    blockIndex++;
                }

                resultBuffer[0] = GZipId1;
                resultBuffer[1] = GZipId2;
                resultBuffer[2] = GZipDeflate;

                _bytesProvided += resultBuffer.Length;
                return new CompressionBlock(_currentBlockIndex, resultBuffer);
            }
        }

        public long TotalBytes
        {
            get
            {
                // Не для всех потоков вернет то, что нужно, но цели академические.
                return _gzippedStream.Length;
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
