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
            stream.OnErrorOccured += Stat_ErrorOccured;
            stream.CopyTo(File.Create(destinationPath));

            return stream;
        }

        public IArchiverStatistics Decompress(string destinationPath)
        {
            MultiThreadGZipStream stream =
                    new MultiThreadGZipStream(
                        File.OpenRead(_sourceFileName),
                        CompressionMode.Decompress);

            stream.OnErrorOccured += Stat_ErrorOccured;
            stream.CopyTo(File.Create(destinationPath));
            return stream;
        }

        private static void Stat_ErrorOccured(object sender, EventArgs<Exception> e)
        {
            Console.WriteLine(Properties.Resources.ErrorOccuredMessage);

            using (TextWriter tsw = new StreamWriter(@"log.txt", true))
            {
                tsw.WriteLine(e.Args.ToString());
            }
        }
    }
}
