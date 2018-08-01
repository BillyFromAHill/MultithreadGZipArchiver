using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class GZipHeader
    {
        private byte[] _data;

        private const byte GZipId1 = 0x1f;
        private const byte GZipId2 = 0x8b;
        private const byte GZipDeflate = 0x08;

        public GZipHeader(Stream gzipStream)
        {
            if (gzipStream == null)
            {
                throw new ArgumentNullException("gzipStream");
            }

            byte[] initialBlock = new byte[10];

            int read = gzipStream.Read(initialBlock, 0, initialBlock.Length);

            if (read < 10 ||
                initialBlock[0] != GZipId1 ||
                initialBlock[1] != GZipId2 ||
                initialBlock[2] != GZipDeflate)
            {
                // В этом месте, в случае неправильного заголовка, вычитанные данные из потока пропадут.
                throw new ArgumentException("Wrong gzip header.");
            }

            int extraSize = 0;
            byte[] extrabyte = null;
            if (IsHasExtra(initialBlock))
            {
                extrabyte = new byte[sizeof(Int16)];
                gzipStream.Read(extrabyte, 0, sizeof(Int16));

                extraSize = BitConverter.ToInt16(extrabyte, 0);
            }

            if (extraSize == 0)
            {
                _data = initialBlock;
                return;
            }

            _data = new byte[initialBlock.Length + extraSize + 2];
            Array.Copy(initialBlock, _data, initialBlock.Length);
            Array.Copy(extrabyte, 0, _data, initialBlock.Length, sizeof(Int16));

            gzipStream.Read(_data, initialBlock.Length + sizeof(Int16), extraSize);
        }

        public byte[] Header
        {
            get
            {
                return _data;
            }
        }

        public GZipHeader(byte[] gzipBlock)
        {
            if (gzipBlock == null)
            {
                throw new ArgumentNullException("gzipBlock");
            }

            if (gzipBlock.Length < 10)
            {
                throw new ArgumentException("gzipBlock");
            }

            if (gzipBlock[0] != GZipId1 || gzipBlock[1] != GZipId2 || gzipBlock[2] != GZipDeflate)
            {
                throw new ArgumentException("Wrong gzip header.");
            }

            int headerSize = 10;

            int extraSize = GetExtraSize(gzipBlock);

            if (extraSize > 0)
            {
                headerSize += 2 + extraSize;
            }

            _data = new byte[headerSize];

            Array.Copy(gzipBlock, 0, _data, 0, headerSize);
        }

        public void SetExtra(byte s1, byte s2, byte[] value)
        {
            byte[] extra = GetExtra(s1, s2);

            int extraPosition = GetExtraPostion(s1, s2);
            if (extraPosition > 0 && extra.Length == value.Length)
            {
                Array.Copy(value, 0, _data, extraPosition + 4, value.Length);
                return;
            }
            else if (extraPosition > 0 && extra.Length != value.Length)
            {
                byte[] newHeader = new byte[_data.Length + value.Length - extra.Length];

                SetExtraSize(newHeader, (Int16)(GetExtraSize(_data) + value.Length - extra.Length));

                Array.Copy(_data, 0, newHeader, 0, extraPosition + 2);
                Int16 newLength = (Int16)value.Length;
                Array.Copy(BitConverter.GetBytes(newLength), 0, newHeader, extraPosition + 2, 2);
                Array.Copy(value, 0, newHeader, extraPosition + 4, value.Length);
                Array.Copy(
                    _data,
                    extraPosition + 4 + extra.Length,
                    newHeader,
                    extraPosition + 4 + value.Length,
                    newHeader.Length - extraPosition + 4 + value.Length);

                _data = newHeader;
            }
            else
            {
                int newHeaderSize = _data.Length + value.Length + 4;
                if (!IsHasExtra(_data))
                {
                    newHeaderSize += 2;
                }

                byte[] newHeader = new byte[newHeaderSize];

                Array.Copy(_data, 0, newHeader, 0, _data.Length);

                Int16 oldExtraSize = GetExtraSize(_data);

                newHeader[3] |= 0b00000100;

                SetExtraSize(newHeader, (short)(oldExtraSize + value.Length + 4));

                int newDataStart =_data.Length;
                if (oldExtraSize == 0)
                {
                    newDataStart += 2;
                }

                newHeader[newDataStart] = s1;
                newHeader[newDataStart + 1] = s2;

                Int16 extaLength = (Int16)value.Length;
                Array.Copy(BitConverter.GetBytes(extaLength), 0, newHeader, newDataStart + 2, 2);
                Array.Copy(value, 0, newHeader, newDataStart + 4, value.Length);

                _data = newHeader;
            }
        }

        public byte[] GetExtra(byte s1, byte s2)
        {
            int dataPosition = GetExtraPostion(s1, s2);
            if (dataPosition < 0)
            {
                return null;
            }

            Int16 length = BitConverter.ToInt16(_data, dataPosition + 2);

            byte[] result = new byte[length];

            Array.Copy(_data, dataPosition + 4, result, 0, length);

            return result;
        }

        private int GetExtraPostion(byte s1, byte s2)
        {
            int dataPosition = -1;
            int xtraSize = GetExtraSize(_data);
            if (GetExtraSize(_data) == 0)
            {
                return dataPosition;
            }

            for (int i = 13; i < 12 + xtraSize; i++)
            {
                if (_data[i - 1] == s1 && _data[i] == s2)
                {
                    dataPosition = i - 1;
                    break;
                }
            }

            return dataPosition;
        }

        private Int16 GetExtraSize(byte[] data)
        {
            if (!IsHasExtra(data))
            {
                return 0;
            }

            return BitConverter.ToInt16(data, 10);
        }


        private void SetExtraSize(byte[] data, Int16 value)
        {
            if (!IsHasExtra(data))
            {
                return;
            }

            Array.Copy(BitConverter.GetBytes(value), 0, data, 10, 2);
        }

        private bool IsHasExtra (byte[] data)
        {
            return (data[3] & 0b00000100) == 0b00000100;
        }
    }
}
