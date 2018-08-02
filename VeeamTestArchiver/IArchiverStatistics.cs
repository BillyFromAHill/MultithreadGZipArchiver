using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Статистика работы архиватора.
    /// </summary>
    public interface IArchiverStatistics
    {
        /// <summary>
        /// Завершено процентов.
        /// </summary>
        double PercentsDone { get; }

        /// <summary>
        /// Завершено ли.
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// Возбуждается при возникновении ошибки в процессе сжатия.
        /// </summary>
        event EventHandler<EventArgs<Exception>> OnErrorOccured;
    }
}
