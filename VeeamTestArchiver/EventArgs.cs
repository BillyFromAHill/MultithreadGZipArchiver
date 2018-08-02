using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    /// <summary>
    /// Костыль для события с произвольными аргументами.
    /// </summary>
    /// <typeparam name="T">
    /// Тип аргумента события.
    /// </typeparam>
    public class EventArgs<T> : EventArgs
    {
        /// <summary>
        /// Аргументы события.
        /// </summary>
        public T Args { get; private set; }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="EventArgs"/>
        /// </summary>
        /// <param name="args">
        /// Аргументы события.
        /// </param>
        public EventArgs(T args)
        {
            Args = args;
        }
    }
}
