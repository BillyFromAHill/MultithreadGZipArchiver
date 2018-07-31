using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    internal interface IBlocksProvider
    {
        CompressionBlock GetNextBlock();
    }
}
