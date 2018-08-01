using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    public interface IArchiverStatistics
    {
        double PercentsDone { get; }

        bool IsDone { get; }

        event EventHandler<EventArgs<Exception>> OnErrorOccured;
    }
}
