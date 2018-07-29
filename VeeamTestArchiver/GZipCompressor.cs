using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    public class GZipCompressor
    {
        private uint _blockSizeBytes;

        private string _sourceFileName;

        public GZipCompressor(string filename, uint blockSize = 1024 * 1024)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("fileName");
            }

            _sourceFileName = filename;
            _blockSizeBytes = blockSize;
        }

        public void Compress(string destinationPath)
        {

        }

        public void Decompress(string destinationPath)
        {

        }
    }
}
