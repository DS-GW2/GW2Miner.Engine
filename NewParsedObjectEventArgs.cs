using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GW2Miner.Engine
{
    /// <summary>
    /// A simple event that notifies when a new object has been parsed, and 
    /// supplies the value for immediate consumption.
    /// </summary>
    public class NewParsedObjectEventArgs<T> : EventArgs
    {
        public T ParsedObject { get; private set; }

        public NewParsedObjectEventArgs(T parsedObject)
        {
            this.ParsedObject = parsedObject;
        }
    }
}
