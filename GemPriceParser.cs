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
    public class GemPriceListGoldToGemsParser
    {
        public Object classLock = typeof(GemPriceListGoldToGemsParser);
        public GemPrice Parse(Stream inputStream)
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
                        //GemPriceListGoldToGems list = (GemPriceListGoldToGems)serializer.Deserialize(jsonTextReader, typeof(GemPriceListGoldToGems));
                        GemPrice list = (GemPrice)serializer.Deserialize(jsonTextReader, typeof(GemPrice));
                        return (list);
                    }
                }
            }
        }
    }

    public class GemPriceListGemsToGoldParser
    {
        public Object classLock = typeof(GemPriceListGemsToGoldParser);
        public GemPrice Parse(Stream inputStream)
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
                        //GemPriceListGemsToGold list = (GemPriceListGemsToGold)serializer.Deserialize(jsonTextReader, typeof(GemPriceListGemsToGold));
                        GemPrice list = (GemPrice)serializer.Deserialize(jsonTextReader, typeof(GemPrice));
                        return (list);
                    }
                }
            }
        }
    }
}
