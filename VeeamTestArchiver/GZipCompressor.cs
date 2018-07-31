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
        private string _sourceFileName;

        public GZipCompressor(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("fileName");
            }

            _sourceFileName = filename;
        }

        public IArchiverStatistics Compress(string destinationPath)
        {
            MultiThreadGZipStream stream =
                new MultiThreadGZipStream(
                    File.OpenRead(_sourceFileName),
                    CompressionMode.Compress);

            stream.CopyTo(File.Create(destinationPath));

            return stream;
        }

        public IArchiverStatistics Decompress(string destinationPath)
        {
            MultiThreadGZipStream stream =
                new MultiThreadGZipStream(
                    File.OpenRead(_sourceFileName),
                    CompressionMode.Decompress);

            stream.CopyTo(File.Create(destinationPath));

            return stream;
        }
    }
}
