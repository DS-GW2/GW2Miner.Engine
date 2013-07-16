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
    //public class ListingParser : IParser<ItemBuySellListingItem>
    public class ListingParser
    {
        public event EventHandler<NewParsedObjectEventArgs<ItemBuySellListingItem>> ObjectParsed;

        private void OnObjectParsed(ItemBuySellListingItem newItemBuySellListingItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<ItemBuySellListingItem>(newItemBuySellListingItem));
            }
        }

        public List<ItemBuySellListingItem> Parse(Stream inputStream, bool getBuyListing)
        {
            JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

            // Create a serializer
            JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

            using (StreamReader streamReader = new StreamReader(inputStream, new UTF8Encoding(false, true)))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                {
                    //jsonTextReader.DateParseHandling = DateParseHandling.None;
                    return (getBuyListing ? ((ItemBuySellListingList)serializer.Deserialize(jsonTextReader, typeof(ItemBuySellListingList))).Listings.BuyListings : 
                        ((ItemBuySellListingList)serializer.Deserialize(jsonTextReader, typeof(ItemBuySellListingList))).Listings.SellListings);
                }
            }
        }
    }
}
