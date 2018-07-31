using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace VeeamTestArchiver
{
    internal interface IBlocksProvider
    {
        CompressionBlock GetNextBlock();

        long TotalBytes { get; }

        long BytesProvided { get; }
    }
}
