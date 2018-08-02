using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Компрессор файлов GZip.
    /// </summary>
    public class GZipCompressor
    {
        private string _sourceFileName;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GZipCompressor"/>
        /// </summary>
        /// <param name="filename">
        /// Имя исходного файла.
        /// </param>
        public GZipCompressor(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException("fileName");
            }

            // Здесь по заголовку можно дополнительно определять сжат ли уже.
            _sourceFileName = filename;
        }

        /// <summary>
        /// Сжимает исходный файл в файл с указанным путем.
        /// </summary>
        /// <param name="destinationPath">
        /// Путь для сжатия.
        /// </param>
        /// <returns>
        /// Возвращает интерфейс доступа к статистике процесса <see cref="IArchiverStatistics"/>.
        /// </returns>
        /// <remarks>
        /// Сразу же возвращает управление. Работает в фоне.
        /// </remarks>
        public IArchiverStatistics Compress(string destinationPath)
        {
            return GetStream(CompressionMode.Compress, destinationPath);
        }

        /// <summary>
        /// Распаковывает исходный файл в файл с указанным путем.
        /// </summary>
        /// <param name="destinationPath">
        /// Путь для распаковке.
        /// </param>
        /// <returns>
        /// Возвращает интерфейс доступа к статистике процесса <see cref="IArchiverStatistics"/>.
        /// </returns>
        /// <remarks>
        /// Сразу же возвращает управление. Работает в фоне.
        /// </remarks>
        public IArchiverStatistics Decompress(string destinationPath)
        {
            return GetStream(CompressionMode.Decompress, destinationPath);
        }

        private IArchiverStatistics GetStream(CompressionMode compressionMode, string fileName)
        {
            MultiThreadGZipStream stream = null;
            try
            {
                stream = new MultiThreadGZipStream(
                    File.OpenRead(_sourceFileName),
                    compressionMode);

                stream.OnErrorOccured += Stat_ErrorOccured;
                stream.CopyTo(File.Create(fileName));
            }
            catch (Exception ex)
            {
                stream.Dispose();
                stream = null;
                LogError(ex);
            }

            return stream;
        }

        private static void LogError(Exception ex)
        {
            Console.WriteLine(Properties.Resources.ErrorOccuredMessage);

            using (TextWriter tsw = new StreamWriter(@"log.txt", true))
            {
                tsw.WriteLine(ex.ToString());
            }
        }

        private static void Stat_ErrorOccured(object sender, EventArgs<Exception> e)
        {
            LogError(e.Args);
        }
    }
}
