using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GW2Miner.Engine
{
    /// <summary>
    /// A class that accepts a stream, and parses it into objects of the appropriate type,
    /// raising events for each new object produced.
    /// </summary>
    /// <typeparam name="TOut">
    /// The type of object that will be parsed.
    /// </typeparam>
    public interface IParser<TOut>
    {
        /// <summary>
        /// Raised whenever a new object is parsed in the Parse method.
        /// </summary>
        event EventHandler<NewParsedObjectEventArgs<TOut>> ObjectParsed;

        /// <summary>
        /// Parse objects of type TOut from the inputStream.
        /// </summary>
        List<TOut> Parse(Stream inputStream);
    }
}
