using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Поставщик блоков декомпрессору.
    /// </summary>
    internal interface IBlocksProvider
    {
        /// <summary>
        /// Выделяет и возвращает следующий блок.
        /// </summary>
        /// <returns>
        /// Следующий блок.
        /// </returns>
        CompressionBlock GetNextBlock();

        /// <summary>
        /// Байт всего.
        /// </summary>
        long TotalBytes { get; }

        /// <summary>
        /// Байт вычитано.
        /// </summary>
        long BytesProvided { get; }
    }
}
