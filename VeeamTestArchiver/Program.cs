using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: Проверять, что разные файлы.
            var gzipCompressor = new GZipCompressor("test.mkv");

            gzipCompressor.Compress("testOut.gz");

        }
    }
}
