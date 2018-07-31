﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace VeeamTestArchiver
{
    class Program
    {
        private static string CompressCommand = "compress";

        private static string DecompressCommand = "decompress";

        private static string DefaultCompress = "test.mkv";

        private static string DefaultDecompress = "test.gz";


        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Any(a => a.ToLower().Equals("-h")))
            {
                Console.WriteLine(Properties.Resources.HelpMessage);
                return;
            }

            string command = string.Empty;
            string sourceFile = string.Empty;
            string destFile = string.Empty;

            if (args.Length > 0)
            {
                command = args[0].ToLower();

                if (!command.Equals(CompressCommand) && !command.Equals(DecompressCommand))
                {
                    Console.WriteLine(Properties.Resources.HelpMessage);
                    return;
                }
            }

            if (args.Length > 1)
            {
                sourceFile = args[1];
            }
            else if (command.Equals(CompressCommand))
            {
                sourceFile = DefaultCompress;
            }
            else
            {
                sourceFile = DefaultDecompress;
            }

            if (args.Length > 2)
            {
                destFile = args[2];
            }
            else if (command.Equals(CompressCommand))
            {
                destFile = DefaultDecompress;
            }
            else
            {
                destFile = DefaultCompress;
            }

            if (Path.GetFullPath(sourceFile) == Path.GetFullPath(destFile))
            {
                Console.WriteLine(Properties.Resources.SourceAndDestMustBeDifferentMessage);
                return;
            }

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine(string.Format(Properties.Resources.SourceDoesNotExistMessage, sourceFile));
                return;
            }

            var gzipCompressor = new GZipCompressor(sourceFile);

            IArchiverStatistics stat;
            if (command.Equals(CompressCommand))
            {
                stat = gzipCompressor.Compress(destFile);
            }
            else
            {
                stat = gzipCompressor.Decompress(destFile);
            }


            while (!stat.IsDone)
            {
                UpdateProgress(stat);
                Thread.Sleep(500);
            }
        }

        private static void UpdateProgress(Object statictics)
        {
            IArchiverStatistics archiverStatistics = statictics as IArchiverStatistics;

            if (archiverStatistics != null)
            {
                Console.Write("\r{0:F}%", archiverStatistics.PercentsDone);
            }
        }
    }
}
