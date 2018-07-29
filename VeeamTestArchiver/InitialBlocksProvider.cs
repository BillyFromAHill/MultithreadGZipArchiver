using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal class InitialBlocksProvider : IDisposable
    {
        private bool disposedValue = false;

        public InitialBlocksProvider(string filename, int blockSize)
        {
            byte[] buf = new byte[1024 * 1024];


            Stopwatch watcher = new Stopwatch();

            watcher.Start();
            using (FileStream stream = File.OpenRead("test.mkv"))
            {
                for (int i = 0; i < 1024; i++)
                {
                    stream.Read(buf, 0, 1024 * 1024);
                }

                buf[20] = 0;
            }

            watcher.Stop();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
