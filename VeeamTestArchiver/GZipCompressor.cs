using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    public class GZipCompressor
    {
        private int _blockSizeBytes;

        private string _sourceFileName;

        public GZipCompressor(string filename, int blockSize = 1024 * 1024)
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
            MultiThreadGZipStream stream =
                new MultiThreadGZipStream(
                    File.OpenRead(_sourceFileName),
                    CompressionMode.Compress,
                    _blockSizeBytes);

            stream.CopyTo(File.Create(destinationPath));
        }

        public void Decompress(string destinationPath)
        {
            MultiThreadGZipStream stream =
                new MultiThreadGZipStream(
                    File.OpenRead(_sourceFileName),
                    CompressionMode.Decompress,
                    _blockSizeBytes);

            stream.CopyTo(File.Create(destinationPath));
        }
    }
}
