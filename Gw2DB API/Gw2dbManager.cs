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
    public class Gw2dbManager
    {
        StreamReader itemsSR, recipesSR, itemsErrataSR, mysticForgeSR;
        readonly string itemsFile = "DB\\items.json";
        readonly string recipesFile = "DB\\recipes.json";
        readonly string itemsErrataFile = "DB\\itemserrata.json";
        readonly string mysticForgeRecipesFile = "DB\\mysticforge.json";

        public Stream RequestGw2dbItems()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, itemsFile);
            if (File.Exists(file))
            {
                itemsSR = new StreamReader(file);

                return itemsSR.BaseStream;
            }

            throw new FileNotFoundException("GW2DB Items JSON File Not Found!", file);
        }

        public Stream RequestGw2dbRecipes()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, itemsFile);
            if (File.Exists(file))
            {
                recipesSR = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, recipesFile));

                return recipesSR.BaseStream;
            }

            throw new FileNotFoundException("GW2DB Recipes JSON File Not Found!", file);
        }

        public Stream RequestGw2dbItemsErrata()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, itemsErrataFile);
            if (File.Exists(file))
            {
                itemsErrataSR = new StreamReader(file);

                return itemsErrataSR.BaseStream;
            }

            throw new FileNotFoundException("GW2DB Items Errata JSON File Not Found!", file);
        }

        public Stream RequestGw2dbMysticForgeRecipes()
        {
            String file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, mysticForgeRecipesFile);
            if (File.Exists(file))
            {
                mysticForgeSR = new StreamReader(file);

                return mysticForgeSR.BaseStream;
            }

            throw new FileNotFoundException("GW2DB Mystic Forge Recipes JSON File Not Found!", file);
        }
    }
}
