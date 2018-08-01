using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VeeamTestArchiver
{
    public class EventArgs<T> : EventArgs
    {
        public T Args { get; private set; }

        public EventArgs(T args)
        {
            Args = args;
        }
    }
}
