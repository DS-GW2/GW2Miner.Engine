using GW2Miner.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GW2Miner.Engine
{
    public class GemPriceParser
    {
        public Object classLock = typeof(GemPriceParser);
        public GemPriceList2 Parse(Stream inputStream)
        {
            lock (classLock)
            {
                JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

                // Create a serializer
                JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

                using (StreamReader streamReader = new StreamReader(inputStream, new UTF8Encoding(false, true)))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                    {
                        //jsonTextReader.DateParseHandling = DateParseHandling.None;
                        GemPriceList list = (GemPriceList)serializer.Deserialize(jsonTextReader, typeof(GemPriceList));
                        return (list.Gems);
                    }
                }
            }
        }
    }
}
