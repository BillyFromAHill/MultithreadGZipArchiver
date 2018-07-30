using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class CompressedBlocksProvider
    {
        // Чтение выделено в объекты, поскольку внутри может быть реализовано чтение файла в несколько потоков,
        // что для RAID может дать прирост скорости.
        // Для чтения сжатых блоков выбран вариант вычитывания до идентификатора gzip.
        // Переупаковка блока, с дополнением в виде размера блока в заголовке, дала бы более быстрое чтение, но
        // проявилось бы это только при чтении упакованного этой же утилитой файла и необходимость
        // реализации вычитывания до следующего заголовка все равно бы не отпала.
        private Stream _gzippedStream;
        private Object _streamLock = new Object();


        private int _currentBlockIndex = -1;

        private const int InternalBufferSize = 1024;

        private byte[] _lastInternalBlock = null;

        private int _lastBlockSize = 0;

        // Байты ID используются как разделитель.
        private int _lastInternalBlockPosition = 2;

        private const byte GZipId1 = 0x1f;
        private const byte GZipId2 = 0x8b;

        public CompressedBlocksProvider(Stream gzippedStream)
        {
            if (gzippedStream == null)
            {
                throw new ArgumentNullException("gzippedStream");
            }

            _gzippedStream = gzippedStream;
        }

        public CompressionBlock GetNextBlock()
        {
            lock (_streamLock)
            {
                byte[] currentBlock = _lastInternalBlock;
                int count = _lastBlockSize;

                if (currentBlock == null)
                {
                    currentBlock = new byte[InternalBufferSize];

                    count = _gzippedStream.Read(currentBlock, 0, InternalBufferSize);

                    if (count == 0)
                    {
                        return null;
                    }

                    if (count > 2 && (currentBlock[0] != GZipId1 || currentBlock[1] != GZipId2))
                    {
                        throw new MissingFieldException("Ids are not found.");
                    }
                }

                _currentBlockIndex++;
                bool firstFound = false;
                bool secondFound = false;

                // Сжатый блок может быть, любого размера, в том числе, больше исходного.
                var blocks = new List<byte[]>();

                int blockStart = _lastInternalBlockPosition;

                do
                {
                    int currentBlockStart = _lastInternalBlockPosition;
                    blocks.Add(currentBlock);
                    for (int i = currentBlockStart; i < count; i++)
                    {
                        if (firstFound && currentBlock[i] == GZipId2)
                        {
                            secondFound = true;
                            _lastInternalBlockPosition = i + 1;
                            break;
                        }

                        firstFound = currentBlock[i] == GZipId1;
                    }

                    if (firstFound && secondFound)
                    {
                        break;
                    }

                    currentBlockStart = 0;
                    _lastBlockSize = count;
                    currentBlock = new byte[InternalBufferSize];
                    count = _gzippedStream.Read(currentBlock, 0, InternalBufferSize);
                }
                while (count > 0);

                if (firstFound && secondFound)
                {
                    _lastBlockSize = _lastInternalBlockPosition - 2;
                }
                else
                {
                    _lastInternalBlockPosition = count;
                }

                long resultBlockSize = (blocks.Count - 1) * InternalBufferSize + _lastBlockSize + 2 - blockStart;

                byte[] resultBuffer = new byte[resultBlockSize];
                int blockIndex = 0;
                long placePosition = 2;
                foreach (var block in blocks)
                {
                    long bytesToCopy = InternalBufferSize;
                    long copyStart = 0;

                    if (blockIndex == 0)
                    {
                        copyStart = blockStart;
                        bytesToCopy = InternalBufferSize - copyStart;
                    }

                    if (blockIndex == blocks.Count - 1)
                    {
                        bytesToCopy = _lastBlockSize - 2;
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

                return new CompressionBlock(_currentBlockIndex, resultBuffer);
            }
        }
    }
}
