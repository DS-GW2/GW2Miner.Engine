using GW2Miner.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GW2Miner.Engine
{
    public class ItemParser
    {
        public Object classLock = typeof(ItemParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public ItemList Parse(Stream inputStream)
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
                        return ((ItemList)serializer.Deserialize(jsonTextReader, typeof(ItemList)));
                    }
                }
            }
        }
    }

    public class gw2spidyItemParser
    {
        public Object classLock = typeof(gw2spidyItemParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public gw2spidyItemList Parse(Stream inputStream)
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
                        return ((gw2spidyItemList)serializer.Deserialize(jsonTextReader, typeof(gw2spidyItemList)));
                    }
                }
            }
        }
    }

    public class gw2spidyOneItemParser
    {
        public Object classLock = typeof(gw2spidyOneItemParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public gw2spidyItemResult Parse(Stream inputStream)
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
                        return ((gw2spidyItemResult)serializer.Deserialize(jsonTextReader, typeof(gw2spidyItemResult)));
                    }
                }
            }
        }
    }

    public class gw2apiItemParser
    {
        public Object classLock = typeof(gw2apiItemParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public Dictionary<int, gw2apiItem> Parse(Stream inputStream)
        {
            lock (classLock)
            {
                JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

                // Create a serializer
                JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

                using (StreamReader streamReader = new StreamReader(inputStream, new UnicodeEncoding(false, true)))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                    {
                        Dictionary<int, gw2apiItem>itemsDict = ((Dictionary<int, gw2apiItem>)serializer.Deserialize(jsonTextReader, typeof(Dictionary<int, gw2apiItem>)));
                        //foreach (gw2apiItem item in itemsDict.Values)
                        //{
                        //    if (item.RarityId >= RarityEnum.Masterwork && ((item.Flags & (GW2APIFlagsEnum.SoulBound_On_Use | GW2APIFlagsEnum.SoulBound_On_Acquire | 
                        //                                                                    GW2APIFlagsEnum.Account_Bound)) == 0))
                        //    {
                        //        item.Flags |= GW2APIFlagsEnum.SoulBound_On_Use;
                        //    }
                        //}

                        return itemsDict;
                    }
                }
            }
        }
    }

    public class gw2apiRecipeParser
    {
        public Object classLock = typeof(gw2apiRecipeParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public Dictionary<int, gw2apiRecipe> Parse(Stream inputStream)
        {
            lock (classLock)
            {
                JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

                // Create a serializer
                JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

                using (StreamReader streamReader = new StreamReader(inputStream, new UnicodeEncoding(false, true)))
                {
                    using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                    {
                        return ((Dictionary<int, gw2apiRecipe>)serializer.Deserialize(jsonTextReader, typeof(Dictionary<int, gw2apiRecipe>)));
                    }
                }
            }
        }
    }

    public class gw2dbItemParser
    {
        public Object classLock = typeof(gw2dbItemParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        //public Task<List<gw2dbItem>> Parse(Stream inputStream)
        //{
        //    lock (classLock)
        //    {
        //        JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

        //        // Create a serializer
        //        JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

        //        // Create task reading the content
        //        return Task.Factory.StartNew(() =>
        //        {
        //            using (StreamReader streamReader = new StreamReader(inputStream, new UTF8Encoding(false, true)))
        //            {
        //                using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
        //                {
        //                    return (List<gw2dbItem>)serializer.Deserialize(jsonTextReader, typeof(List<gw2dbItem>));
        //                }
        //            }
        //        });
        //    }
        //}

        public List<gw2dbItem> Parse(Stream inputStream)
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
                        return ((List<gw2dbItem>)serializer.Deserialize(jsonTextReader, typeof(List<gw2dbItem>)));
                    }
                }
            }
        }
    }

    public class gw2dbRecipeParser
    {
        public Object classLock = typeof(gw2dbRecipeParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        //public Task<List<gw2dbRecipe>> Parse(Stream inputStream)
        //{
        //    lock (classLock)
        //    {
        //        JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings();

        //        // Create a serializer
        //        JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

        //        // Create task reading the content
        //        return Task.Factory.StartNew(() =>
        //        {
        //            using (StreamReader streamReader = new StreamReader(inputStream, new UTF8Encoding(false, true)))
        //            {
        //                using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
        //                {
        //                    return (List<gw2dbRecipe>)serializer.Deserialize(jsonTextReader, typeof(List<gw2dbRecipe>));
        //                }
        //            }
        //        });
        //    }
        //}

        public List<gw2dbRecipe> Parse(Stream inputStream)
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
                        return ((List<gw2dbRecipe>)serializer.Deserialize(jsonTextReader, typeof(List<gw2dbRecipe>)));
                    }
                }
            }
        }
    }

    public class ItemListParser
    {
        public Object classLock = typeof(ItemListParser);
        public event EventHandler<NewParsedObjectEventArgs<Item>> ObjectParsed;

        private void OnObjectParsed(Item newItem)
        {
            if (this.ObjectParsed != null)
            {
                this.ObjectParsed(this, new NewParsedObjectEventArgs<Item>(newItem));
            }
        }

        public MyItemList Parse(Stream inputStream)
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
                        MyItemList itemList = (MyItemList)serializer.Deserialize(jsonTextReader, typeof(MyItemList));

                        foreach (Item item in itemList.Items)
                        {
                            item.Created = item.Created.ToLocalTime();
                            item.Purchased = item.Purchased.ToLocalTime();
                        }

                        return itemList;
                    }
                }
            }
        }
    }
}
