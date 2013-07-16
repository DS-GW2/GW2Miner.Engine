using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Configuration;
using GW2SessionKey;

namespace GW2Miner.Engine
{
    public class Gw2apiManager
    {
        StreamReader itemsSR, recipesSR;
        readonly string itemsFile = "DB\\itemsapi.json";
        readonly string recipesFile = "DB\\recipesapi.json";

        public Stream RequestGw2apiItems()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, itemsFile);
            if (File.Exists(file))
            {
                itemsSR = new StreamReader(file, Encoding.Unicode);

                return itemsSR.BaseStream;
            }

            throw new FileNotFoundException("GW2API Items JSON File Not Found!", file);
        }

        public Stream RequestGw2apiRecipes()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, recipesFile);
            if (File.Exists(file))
            {
                recipesSR = new StreamReader(file, Encoding.Unicode);

                return recipesSR.BaseStream;
            }

            throw new FileNotFoundException("GW2API Recipes JSON File Not Found!", file);
        }
    }
}
