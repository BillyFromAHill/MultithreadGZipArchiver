using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Поставщик сжатых блоков. 
    /// </summary>
    internal class CompressedBlocksProvider : IBlocksProvider
    {
        // Чтение выделено в объекты, поскольку внутри может быть реализовано чтение файла в несколько потоков,
        // что для RAID, например, может дать прирост скорости.
        // Для чтения сжатых блоков выбран вариант вычитывания до идентификатора gzip.
        // Переупаковка блока, с дополнением в виде размера блока в заголовке, дала бы более быстрое чтение, но
        // проявилось бы это только при чтении упакованного этой же утилитой файла и необходимость
        // реализации вычитывания до следующего заголовка все равно бы не отпала.

        // Update: Не взлетело. Нужно писать дополнительно в заголовок для параллельной распаковки.
        private Stream _gzippedStream;
        private Object _streamLock = new Object();

        private long _bytesProvided = 0;
        private int _currentBlockIndex = -1;

        private  int _internalBufferSize = 1024 * 1024;

        private bool _isBlocksDelimited = false;

        /// <summary>
        /// Идентификатор дополнительного поля gzip SI1.
        /// </summary>
        public static byte VeeamArchiverSI1 = 1;

        /// <summary>
        /// Идентификатор дополнительного поля gzip SI2.
        /// </summary>
        public static byte VeeamArchiverSI2 = 4;

        /// <summary>
        /// Инициализирует новый экзепляр класса <see cref="CompressedBlocksProvider"/>
        /// </summary>
        /// <param name="gzippedStream">
        /// Поток для вычитывания блоков.
        /// </param>
        /// <param name="internalBufferSize">
        /// Размер внутреннего буфера для вычитывания.
        /// </param>
        public CompressedBlocksProvider(Stream gzippedStream, int internalBufferSize = 1024 * 1024)
        {
            if (gzippedStream == null)
            {
                throw new ArgumentNullException("gzippedStream");
            }

            _internalBufferSize = internalBufferSize;
            _gzippedStream = gzippedStream;
        }

        /// <inheritdoc />
        public CompressionBlock GetNextBlock()
        {
            lock (_streamLock)
            {
                int readSize = _internalBufferSize;
                GZipHeader header = null;
                _currentBlockIndex++;
                byte[] resultBuffer;
                int bytesRead = 0;

                if (_gzippedStream.Position == _gzippedStream.Length)
                {
                    return null;
                }

                // Для первого буфера нужно вычитать заголовок, который гарантировано должен присутствовать.
                // Дальше принимается решение о том, записан файл данной утилитой и можно вычитывать поблочно или
                // блоки могут быть произвольными, поскольку границы блоков не выделить.
                if (_currentBlockIndex == 0)
                {
                    header = new GZipHeader(_gzippedStream);

                    byte[] sizeData = header.GetExtra(1, 4);
                    if (sizeData != null && BitConverter.ToInt32(sizeData, 0) > 0)
                    {
                        _isBlocksDelimited = true;

                        readSize = BitConverter.ToInt32(sizeData, 0);
                        resultBuffer = new byte[readSize];
                        Array.Copy(header.Header, resultBuffer, header.Header.Length);
                        _gzippedStream.Read(
                            resultBuffer,
                            header.Header.Length,
                            readSize - header.Header.Length);

                        _bytesProvided += resultBuffer.Length;
                        return new CompressionBlock(_currentBlockIndex, resultBuffer);
                    }
                    else
                    {
                        resultBuffer = new byte[_internalBufferSize];
                        Array.Copy(header.Header, resultBuffer, header.Header.Length);
                        bytesRead = _gzippedStream.Read(
                            resultBuffer,
                            header.Header.Length,
                            _internalBufferSize - header.Header.Length);

                        _bytesProvided += bytesRead + header.Header.Length;
                        return new CompressionBlock(
                            _currentBlockIndex, resultBuffer, bytesRead + header.Header.Length );
                    }
                }

                if (_isBlocksDelimited)
                {
                    header = new GZipHeader(_gzippedStream);
                    byte[] sizeData = header.GetExtra(1, 4);
                    if (sizeData != null && BitConverter.ToInt32(sizeData, 0) > 0)
                    {
                        readSize = BitConverter.ToInt32(sizeData, 0);
                    }

                    resultBuffer = new byte[readSize];
                    Array.Copy(header.Header, resultBuffer, header.Header.Length);
                    _gzippedStream.Read(
                        resultBuffer,
                        header.Header.Length,
                        readSize - header.Header.Length);

                    bytesRead = readSize;
                }
                else
                {
                    resultBuffer = new byte[_internalBufferSize];
                    bytesRead = _gzippedStream.Read(
                        resultBuffer,
                        0,
                        _internalBufferSize);
                }

                if (bytesRead == 0)
                {
                    return null;
                }

                _bytesProvided += bytesRead;
                return new CompressionBlock(_currentBlockIndex, resultBuffer, bytesRead);
            }
        }

        /// <inheritdoc />
        public long TotalBytes
        {
            get
            {
                // Не для всех потоков вернет то, что нужно, но цели академические.
                return _gzippedStream.Length;
            }
        }

        /// <inheritdoc />
        public long BytesProvided
        {
            get
            {
                return _bytesProvided;
            }
        }
    }
}
