using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using GW2Miner.Domain;
using System.Diagnostics;
using System.Configuration;
using ServiceStack.Redis;
using ServiceStack.Redis.Generic;

namespace GW2Miner.Engine
{
    public class TradeWorker
    {
        public struct GemPriceTP
        {
            public int gem_to_gold;
            public int gold_to_gem;
        }

        public struct Args
        {
            public int max;
            public int count;
            public int offset;
        }

        public static bool gettingSessionKey = false;

        private readonly int MAX_GW2SPIDY_RETRIES = 3; // max retries for gw2spidy call before giving up
        private bool _useGW2Spidy = true; // use gw2spidy for min acquisition cost routine
        private int _gw2SpidyRetries = 0;

        private static Object classLock = typeof(TradeWorker);
        private static Object _CreatedIdToRecipeLocked = new Object();
        private static Gw2apiManager _gw2apim;
        private static Gw2dbManager _dbm;
        private ConnectionManager _cm;
        private Gw2spidyManager _sm;
        private Args searchArgs;
        private Args transArgs;
        private event EventHandler _fnCallGW2LoginInstructions;
        private Object _fnCallGW2LoginInstructionsLock = typeof(TradeWorker);
        private event EventHandler _fnGW2Logined;
        private Object _fnGW2LoginedLocked = new Object();
        private Object _MinAcquisitionCostLocked = new Object();

        //private static List<gw2dbItem> gw2dbItemList;
        //public static List<gw2dbRecipe> gw2dbRecipeList;
        public static Dictionary<int, gw2dbRecipe> createdIdToRecipe = new Dictionary<int, gw2dbRecipe>();
        private static Dictionary<int, gw2dbItem> dataIdToItem = new Dictionary<int, gw2dbItem>();
        private static bool gw2dbLoaded = false, gw2apiLoaded = false;

        public static Dictionary<int, gw2apiRecipe> gw2api_recipeIdToRecipe = new Dictionary<int, gw2apiRecipe>();
        private static Dictionary<int, gw2apiItem> gw2api_dataIdToItem = new Dictionary<int, gw2apiItem>();

        static TradeWorker()
        {
            _gw2apim = new Gw2apiManager();
            LoadGw2API();
            //T.Wait();

            _dbm = new Gw2dbManager();
            LoadGw2DB();
            //L.Wait();
        }

        public TradeWorker()
        {
            _cm = ConnectionManager.Instance;
            _sm = new Gw2spidyManager();
        }

        private static async Task LoadGw2API()
        {

            gw2apiItemParser itemParser = new gw2apiItemParser();
            gw2api_dataIdToItem = itemParser.Parse(_gw2apim.RequestGw2apiItems());

            gw2apiRecipeParser recipeParser = new gw2apiRecipeParser();
            gw2api_recipeIdToRecipe = recipeParser.Parse(_gw2apim.RequestGw2apiRecipes());

            gw2apiLoaded = true;
        }

        public bool GW2APILoaded
        {
            get
            {
                return gw2apiLoaded;
            }
        }

        public gw2apiItem GetGW2APIItem(int dataId)
        {
            if (gw2api_dataIdToItem.ContainsKey(dataId))
            {
                return gw2api_dataIdToItem[dataId];
            }
            return null;
        }

        public int GW2APIGetItemUpgrade(int dataId)
        {
            int upgradeItemId = 0;

            if (gw2api_dataIdToItem.ContainsKey(dataId))
            {
                switch (gw2api_dataIdToItem[dataId].TypeId)
                {
                    case TypeEnum.Armor:
                        upgradeItemId = gw2api_dataIdToItem[dataId].Armor.UpgradeId ?? 0;
                        break;
                    case TypeEnum.Weapon:
                        upgradeItemId = gw2api_dataIdToItem[dataId].Weapon.UpgradeId ?? 0;
                        break;
                    case TypeEnum.Back:
                        upgradeItemId = gw2api_dataIdToItem[dataId].Back.UpgradeId ?? 0;
                        break;
                    case TypeEnum.Trinket:
                        upgradeItemId = gw2api_dataIdToItem[dataId].Trinket.UpgradeId ?? 0;
                        break;
                }
            }

            return upgradeItemId;
        }

        private static async Task LoadGw2DB()
        {
            Dictionary<int, int> itemIdToDataId = new Dictionary<int, int>();

            gw2dbItemParser itemParser = new gw2dbItemParser();
            //await itemParser.Parse(_dbm.RequestGw2dbItems()).ContinueWith(
            //                            (parseTask) =>
            //                            {
            //                                if (parseTask.IsCanceled)
            //                                {
            //                                    return;
            //                                }
            //                                if (parseTask.IsFaulted)
            //                                {
            //                                    throw parseTask.Exception;
            //                                }

            //                                gw2dbItemList = parseTask.Result;

            //                                foreach (gw2dbItem item in gw2dbItemList)
            //                                {
            //                                    itemIdToDataId.Add(item.Id, item.data_Id);
            //                                    dataIdToItem.Add(item.data_Id, item);
            //                                }
            //                            });
            List<gw2dbItem> gw2dbItemList = itemParser.Parse(_dbm.RequestGw2dbItems());
            List<gw2dbItem> gw2dbItemErrataList = itemParser.Parse(_dbm.RequestGw2dbItemsErrata());

            foreach (gw2dbItem item in gw2dbItemErrataList)
            {
                if (item != null)
                {
                    var index = gw2dbItemList.FindIndex(x => x.data_Id == item.data_Id);
                    // Override everything EXCEPT for the Id, because the Id number may change with each 
                    // release of items.json
                    int temp = gw2dbItemList[index].Id;
                    gw2dbItemList[index] = item;
                    gw2dbItemList[index].Id = temp;
                }
            }

            foreach (gw2dbItem item in gw2dbItemList)
            {
                itemIdToDataId.Add(item.Id, item.data_Id);
                dataIdToItem.Add(item.data_Id, item);
            }

            gw2dbRecipeParser recipeParser = new gw2dbRecipeParser();
            //await recipeParser.Parse(_dbm.RequestGw2dbRecipes()).ContinueWith(
            //                                            (parseTask) =>
            //                                            {
            //                                                if (parseTask.IsCanceled)
            //                                                {
            //                                                    return;
            //                                                } 
            //                                                if (parseTask.IsFaulted)
            //                                                {
            //                                                    throw parseTask.Exception;
            //                                                }

            //                                                List<gw2dbRecipe> gw2dbRecipeList = parseTask.Result;

            //                                                foreach (gw2dbRecipe recipe in gw2dbRecipeList)
            //                                                {
            //                                                    recipe.CreatedDataId = itemIdToDataId[recipe.CreatedItemId];
            //                                                    foreach (gw2dbIngredient ingredient in recipe.Ingredients)
            //                                                    {
            //                                                        ingredient.data_Id = itemIdToDataId[ingredient.Id];
            //                                                    }
            //                                                    //if (!createdIdToRecipe.ContainsKey(recipe.CreatedItemId))
            //                                                    //{
            //                                                    //    createdIdToRecipe.Add(recipe.CreatedItemId, recipe);
            //                                                    //}
            //                                                    dataIdToItem[recipe.CreatedDataId].Recipe = recipe;
            //                                                }

            //                                                gw2dbLoaded = true;
            //                                            });
            List<gw2dbRecipe> gw2dbRecipeList = recipeParser.Parse(_dbm.RequestGw2dbRecipes());
            List<gw2dbRecipe> gw2dbMFRecipeList = recipeParser.Parse(_dbm.RequestGw2dbMysticForgeRecipes());

            ProcessRecipes(gw2dbRecipeList, itemIdToDataId);
            ProcessRecipes(gw2dbMFRecipeList, itemIdToDataId, true);

            //foreach (gw2dbRecipe recipe in gw2dbRecipeList)
            //{
            //    recipe.CreatedDataId = itemIdToDataId[recipe.CreatedItemId];
            //    foreach (gw2dbIngredient ingredient in recipe.Ingredients)
            //    {
            //        gw2dbRecipe ingredientRecipe = new gw2dbRecipe()
            //        {
            //            CreatedDataId = itemIdToDataId[ingredient.Id],
            //            CreatedItemId = ingredient.Id,
            //            Quantity = ingredient.Quantity,
            //        };
            //        ingredientRecipe.Name = dataIdToItem[ingredientRecipe.CreatedDataId].Name;
            //        recipe.IngredientRecipes.Add(ingredientRecipe); // Add 1 layer deep ingredient into each recipe
            //    }
            //    dataIdToItem[recipe.CreatedDataId].Recipes.Add(recipe);
            //    if (!CreatedIdToRecipe.ContainsKey(recipe.CreatedDataId))
            //    {
            //        CreatedIdToRecipe.Add(recipe.CreatedDataId, recipe);
            //    }
            //}

            // Iterate through all the recipes in each item and build up deeper layers of ingredients
            foreach (gw2dbItem item in gw2dbItemList)
            {
                foreach (gw2dbRecipe recipe in item.Recipes)
                {
                    if (recipe != null)
                    {
                        foreach (gw2dbRecipe ingredient in recipe.IngredientRecipes)
                        {
                            ingredient.IngredientRecipes = BuildRecipe(ingredient);
                        }
                    }
                }
            }

            gw2dbLoaded = true;
        }

        public static void ProcessRecipes(List<gw2dbRecipe> gw2dbRecipeList, Dictionary<int, int> itemIdToDataId, bool noConvertDataIds = false)
        {
            foreach (gw2dbRecipe recipe in gw2dbRecipeList)
            {
                if (recipe != null)
                {
                    if (!noConvertDataIds)
                    {
                        //if (!itemIdToDataId.ContainsKey(recipe.CreatedItemId)) Debugger.Break();
                        recipe.CreatedDataId = itemIdToDataId[recipe.CreatedItemId];
                    }
                    foreach (gw2dbIngredient ingredient in recipe.Ingredients)
                    {
                        gw2dbRecipe ingredientRecipe = new gw2dbRecipe()
                        {
                            CreatedItemId = ingredient.Id,
                            Quantity = ingredient.Quantity,
                        };
                        //if (!noConvertDataIds && !itemIdToDataId.ContainsKey(ingredient.Id)) Debugger.Break();
                        ingredientRecipe.CreatedDataId = (noConvertDataIds ? ingredientRecipe.CreatedItemId : itemIdToDataId[ingredient.Id]);
                        ingredientRecipe.Name = dataIdToItem[ingredientRecipe.CreatedDataId].Name;
                        recipe.IngredientRecipes.Add(ingredientRecipe); // Add 1 layer deep ingredient into each recipe
                    }
                    dataIdToItem[recipe.CreatedDataId].Recipes.Add(recipe);
                    if (!CreatedIdToRecipe.ContainsKey(recipe.CreatedDataId))
                    {
                        CreatedIdToRecipe.Add(recipe.CreatedDataId, recipe);
                    }
                }
            }
        }

        public static List<gw2dbRecipe> BuildRecipe(gw2dbRecipe recipe)
        {
            gw2dbItem item = dataIdToItem[recipe.CreatedDataId];

            foreach (gw2dbRecipe itemRecipe in item.Recipes)
            {
                List<gw2dbRecipe> retList = new List<gw2dbRecipe>();
                foreach (gw2dbRecipe ingredient in itemRecipe.IngredientRecipes)
                {
                    // Make a COPY of the ingredients before returning them
                    gw2dbRecipe ingredientRecipe = new gw2dbRecipe()
                    {
                        CreatedDataId = ingredient.CreatedDataId,
                        CreatedItemId = ingredient.Id,
                        Quantity = (int)Math.Round((recipe.Quantity * ingredient.Quantity) / (double)itemRecipe.Quantity),
                        Name = ingredient.Name,
                    };
                    if (ingredientRecipe.Quantity <= 0) ingredientRecipe.Quantity = 1;
                    // Prevent stack overflow recursion
                    if (ingredientRecipe.CreatedDataId != itemRecipe.CreatedDataId) ingredientRecipe.IngredientRecipes = BuildRecipe(ingredientRecipe);
                    retList.Add(ingredientRecipe);
                }
                return retList;
            }

            return null;
        }

        public bool GW2DBLoaded
        {
            get
            {
                return gw2dbLoaded;
            }
        }

        public gw2dbItem GetGW2DBItem(int dataId)
        {
            if (dataIdToItem.ContainsKey(dataId))
            {
                return dataIdToItem[dataId];
            }
            return null;
        }

        public gw2dbItem FindMyRecipeItem(int recipeId)
        {
            foreach (gw2apiItem apiItem in gw2api_dataIdToItem.Values)
            {
                if (apiItem.TypeId == TypeEnum.Consumable && apiItem.Consumable.SubTypeId == ConsumableSubTypeEnum.Unlock && 
                        apiItem.Consumable.UnlockType == GW2APIUnlockTypeEnum.Crafting_Recipe && apiItem.Consumable.RecipeId == recipeId)
                {
                    return GetGW2DBItem(apiItem.Id);
                }
            }

            return null;
        }

        public RecipeCraftingCost MinCraftingCost(List<gw2dbRecipe> recipes)
        {
            RecipeCraftingCost minRecipeCost = new RecipeCraftingCost { GoldCost = int.MaxValue, KarmaCost = int.MaxValue, SkillPointsCost = float.MaxValue };
            if (recipes != null)
            {
                foreach (gw2dbRecipe recipe in recipes)
                {
                    RecipeCraftingCost recipeCost = MinCraftingCost(recipe);
                    if ((recipeCost.GoldCost < minRecipeCost.GoldCost) ||
                            ((recipeCost.GoldCost == minRecipeCost.GoldCost) && (recipeCost.KarmaCost < minRecipeCost.KarmaCost)) || 
                            ((recipeCost.GoldCost == minRecipeCost.GoldCost) && (recipeCost.KarmaCost == minRecipeCost.KarmaCost) && 
                                (recipeCost.SkillPointsCost < minRecipeCost.SkillPointsCost)))
                    {
                        minRecipeCost = recipeCost;
                    }
                }
            }
            if (minRecipeCost.GoldCost == int.MaxValue)
            {
                minRecipeCost.GoldCost = minRecipeCost.KarmaCost = 0;
                minRecipeCost.SkillPointsCost = 0.0f;
            }
            return minRecipeCost;
        }

        public RecipeCraftingCost MinCraftingCost(gw2dbRecipe recipe)
        {
            if (!gw2dbLoaded || recipe == null || recipe.IngredientRecipes == null || recipe.IngredientRecipes.Count == 0)
            {
                return null;
            }           
            else
            {
                if (recipe.MinCraftingCost.GoldCost > 0 || recipe.MinCraftingCost.KarmaCost > 0 || recipe.MinCraftingCost.SkillPointsCost > 0.0)
                {
                    DateTime lastUpdated = recipe.CraftingCostLastUpdated;
                    TimeSpan span = DateTime.Now - lastUpdated;
                    if (span.TotalMinutes <= this.RecipeUpdatedTimeSpanInMinutes)
                    {
                        return recipe.MinCraftingCost;
                    }
                }

                recipe.MinCraftingCost.GoldCost = 0;
                recipe.MinCraftingCost.KarmaCost = 0;
                recipe.MinCraftingCost.SkillPointsCost = 0.0f;
                foreach (gw2dbRecipe ingredient in recipe.IngredientRecipes)
                {
                    RecipeCraftingCost recipeCraftingCost = MinAcquisitionCost(ingredient);
                    if (recipeCraftingCost != null)
                    {
                        recipe.MinCraftingCost.GoldCost = recipe.MinCraftingCost.GoldCost + recipeCraftingCost.GoldCost;
                        recipe.MinCraftingCost.KarmaCost = recipe.MinCraftingCost.KarmaCost + recipeCraftingCost.KarmaCost;
                        recipe.MinCraftingCost.SkillPointsCost = recipe.MinCraftingCost.SkillPointsCost + recipeCraftingCost.SkillPointsCost;
                    }
                }

                recipe.CraftingCostLastUpdated = DateTime.Now;
                ////recipe.CraftingCost.GoldCost = craftingCost;
                return recipe.MinCraftingCost;
            }
        }

        public bool UseGW2SpidyForCraftingCost
        {
            get { return _useGW2Spidy; }
            set { _useGW2Spidy = value; }
        }

        public RecipeCraftingCost MinAcquisitionCost(gw2dbRecipe recipe)
        {
            if (!gw2dbLoaded || recipe == null) return new RecipeCraftingCost();

            // Optimization: We only get the min sale unit price if the last price is at least 5 mins ago
            lock (_MinAcquisitionCostLocked)
            {
                DateTime lastUpdated = recipe.TPLastUpdated;
                if (CreatedIdToRecipe.ContainsKey(recipe.CreatedDataId))
                {
                    lastUpdated = CreatedIdToRecipe[recipe.CreatedDataId].TPLastUpdated;
                }
                else
                {
                    CreatedIdToRecipe.Add(recipe.CreatedDataId, recipe);
                }
                TimeSpan span = DateTime.Now - lastUpdated;
                if (span.TotalMinutes > this.RecipeUpdatedTimeSpanInMinutes)
                {
                    gw2spidyItem spidyItem = null;
                    List<Item> items = null;
                    try
                    {
                        if (_useGW2Spidy)
                        {
                            spidyItem = this.get_gw2spidy_item(recipe.CreatedDataId).Result;

                            _gw2SpidyRetries = 0;  // reset counter

                            recipe.CreatedItemMinSaleUnitPrice = spidyItem.MinSaleUnitPrice;
                            recipe.CreatedItemMaxBuyUnitPrice = spidyItem.MaxOfferUnitPrice;
                            recipe.CreatedItemVendorBuyUnitPrice = 0;
                        }
                    }
                    catch
                    {
                    }

                    if (spidyItem == null)
                    {
                        if (_useGW2Spidy)
                        {
                            _gw2SpidyRetries++; // increment counter for gw2spidy failures
                            if (_gw2SpidyRetries >= MAX_GW2SPIDY_RETRIES) _useGW2Spidy = false; // give up and turn off gw2spidy
                        }

                        items = this.get_items(recipe.CreatedDataId).Result;

                        if (items != null && items.Count > 0)
                        {
                            recipe.CreatedItemMinSaleUnitPrice = items[0].MinSaleUnitPrice;
                            recipe.CreatedItemMaxBuyUnitPrice = items[0].MaxOfferUnitPrice;
                            recipe.CreatedItemVendorBuyUnitPrice = items[0].VendorPrice * 8; // Doesn't matter as code after doesn't make use of this value now
                        }
                        else
                        {
                            recipe.CreatedItemMinSaleUnitPrice = 0;
                            recipe.CreatedItemMaxBuyUnitPrice = 0;
                            recipe.CreatedItemVendorBuyUnitPrice = 0;
                        }
                    }

                    CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemMinSaleUnitPrice = recipe.CreatedItemMinSaleUnitPrice;
                    CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemMaxBuyUnitPrice = recipe.CreatedItemMaxBuyUnitPrice;
                    CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemVendorBuyUnitPrice = recipe.CreatedItemVendorBuyUnitPrice;

                    recipe.TPLastUpdated = DateTime.Now;
                    CreatedIdToRecipe[recipe.CreatedDataId].TPLastUpdated = recipe.TPLastUpdated;
                }
                else
                {
                    recipe.CreatedItemMinSaleUnitPrice = CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemMinSaleUnitPrice;
                    recipe.CreatedItemMaxBuyUnitPrice = CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemMaxBuyUnitPrice;
                    recipe.CreatedItemVendorBuyUnitPrice = CreatedIdToRecipe[recipe.CreatedDataId].CreatedItemVendorBuyUnitPrice;
                }
            }
            int vendorUnitGoldCost = MinBulkAcquisitionUnitGoldCost(recipe.CreatedDataId);
            bool buyableFromVendor;
            if (vendorUnitGoldCost > 0)
            {
                recipe.CreatedItemVendorBuyUnitPrice = vendorUnitGoldCost;
                buyableFromVendor = true;
            }
            else
            {
                buyableFromVendor = false;
                //buyableFromVendor = (recipe.CreatedItemVendorBuyUnitPrice > 0 && recipe.CreatedItemMaxBuyUnitPrice <= recipe.CreatedItemVendorBuyUnitPrice); // HACK!  Till we get better data
                //bool buyableFromVendor = (vendorCost > 0);
            }

            int buyCost = recipe.Quantity * recipe.CreatedItemMinSaleUnitPrice;
            int vendorCost = recipe.Quantity * recipe.CreatedItemVendorBuyUnitPrice;
            RecipeCraftingCost recipeCraftingCost = MinCraftingCost(recipe);
            int craftingCost = (recipeCraftingCost != null) ? recipeCraftingCost.GoldCost : 0;

            recipe.BestMethod = ObtainableMethods.Buy;
            int minCost = buyCost;
            if (buyableFromVendor && vendorCost < buyCost)
            {
                recipe.BestMethod = ObtainableMethods.Vendor;
                minCost = vendorCost;
            }
            if (craftingCost > 0 && (craftingCost < minCost || minCost <= 0))
            {
                recipe.BestMethod = ObtainableMethods.Craft;
                minCost = craftingCost;
            }

            RecipeCraftingCost recipeCost = new RecipeCraftingCost { GoldCost = minCost, KarmaCost = (recipeCraftingCost != null && recipe.BestMethod == ObtainableMethods.Craft) ? recipeCraftingCost.KarmaCost : 0,
                                                                     SkillPointsCost = (recipeCraftingCost != null && recipe.BestMethod == ObtainableMethods.Craft) ? recipeCraftingCost.SkillPointsCost : 0.0f };

            if (minCost <= 0)
            {
                int karmaCost = MinBulkAcquisitionUnitKarmaCost(recipe.CreatedDataId);
                if (karmaCost > 0)
                {
                    recipe.BestMethod = ObtainableMethods.Karma;
                    recipe.CreatedItemMinKarmaUnitPrice = karmaCost;
                    recipeCost.KarmaCost = recipeCost.KarmaCost + (recipe.Quantity * recipe.CreatedItemMinKarmaUnitPrice);
                    minCost = 0; // karma is 0 gold
                }
                else
                {
                    float skillPointsCost = MinBulkAcquisitionUnitSkillPointCost(recipe.CreatedDataId);
                    if (skillPointsCost > 0.0f)
                    {
                        recipe.BestMethod = ObtainableMethods.SkillPoints;
                        recipe.CreatedItemMinSkillPointsUnitPrice = skillPointsCost;
                        recipeCost.SkillPointsCost = recipeCost.SkillPointsCost + (recipe.Quantity * recipe.CreatedItemMinSkillPointsUnitPrice);
                        minCost = 0; // skill point is 0 gold
                    }
                    else recipe.BestMethod = ObtainableMethods.Unknown;
                }
            }            

            return recipeCost;
        }

        public async Task<List<Item>> search_items(string text = "", bool allPages = true, TypeEnum type = TypeEnum.All, int subType = -1, RarityEnum rarity = RarityEnum.All, 
                                                    int levelMin = 0, int levelMax = 80, bool removeUnavailable = true, int offset = 1, int count = 10, string orderBy = "", 
                                                    bool sortDescending = false)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                String query = String.Format("text={0}&type={1}&subtype={2}&rarity={3}&levelmin={4}&levelmax={5}&removeunavailable={6}", text,
                                                                                                                        (type == TypeEnum.All ? "" : ((int)type).ToString()),
                                                                                                                        (subType < 0 ? "" : subType.ToString()),
                                                                                                                        (rarity == RarityEnum.All ? "" : ((int)rarity).ToString()),
                                                                                                                        levelMin, levelMax, removeUnavailable);
                if (orderBy != String.Empty)
                {
                    query = String.Concat(query, String.Format("&orderby={0}", orderBy));

                    if (sortDescending) query = String.Concat(query, String.Format("&sortdescending=1"));
                }

                return await Search(query, allPages, offset, count);
            }
            return new List<Item>();
        }

        public async Task<List<Item>> get_items(params int[] item_ids)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                String itemsString = String.Join(",", item_ids);
                String query = String.Format("ids={0}", itemsString);

                //return await Search(query, true, 1, item_ids.Count());
                return await Items(query);
            }
            return new List<Item>();
        }

        public async Task<List<Item>> get_search_typeahead(string text, TypeEnum type = TypeEnum.All, int subType = -1, RarityEnum rarity = RarityEnum.All,
                                                    int levelMin = 0, int levelMax = 80, bool removeUnavailable = true)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                //String query = String.Format("text={0}&typeahead=1", text);
                String query = String.Format("text={0}&type={1}&subtype={2}&rarity={3}&levelmin={4}&levelmax={5}&removeunavailable={6}&typeahead=1", text,
                                                                                                            (type == TypeEnum.All ? "" : ((int)type).ToString()),
                                                                                                            (subType < 0 ? "" : subType.ToString()),
                                                                                                            (rarity == RarityEnum.All ? "" : ((int)rarity).ToString()),
                                                                                                            levelMin, levelMax, removeUnavailable);

                return await Search(query, false);
            }
            return new List<Item>();
        }

        //public async Task<List<Item>> get_rich_items(params int[] item_ids)
        //{
        //    List<Item> itemList = await get_items(item_ids);

        //    foreach (Item item in itemList)
        //    {
        //        try
        //        {
        //            gw2spidyItem gw2spidyItem = await get_gw2spidy_item(item.Id);

        //            item.TypeId = gw2spidyItem.TypeId;
        //            item.SubTypeId = gw2spidyItem.SubTypeId;
        //            item.PriceLastChanged = gw2spidyItem.PriceLastChanged;
        //            item.SalePriceChangedLastHour = gw2spidyItem.SalePriceChangedLastHour;
        //            item.OfferPriceChangedLastHour = gw2spidyItem.OfferPriceChangedLastHour;
        //            item.GW2DBExternalId = gw2spidyItem.GW2DBExternalId;
        //            item.IsRich = true;
        //        }
        //        catch (Exception e)
        //        {
        //            if (!_cm.CatchExceptions)
        //            {
        //                Console.WriteLine("Exception getting item from Gw2Spidy: {0}", e.Message);
        //            }
        //        }
        //    }

        //    return itemList;
        //}

        public async Task<Item> make_rich_item(Item item)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                bool gw2spidyException = false;

                try
                {
                    if (!item.IsRich)
                    {
                        gw2spidyItem gw2spidyItem = await get_gw2spidy_item(item.Id);

                        item.TypeId = gw2spidyItem.TypeId;
                        item.SubTypeId = gw2spidyItem.SubTypeId;
                        item.PriceLastChanged = gw2spidyItem.PriceLastChanged;
                        item.SalePriceChangedLastHour = gw2spidyItem.SalePriceChangedLastHour;
                        item.OfferPriceChangedLastHour = gw2spidyItem.OfferPriceChangedLastHour;
                        item.GW2DBExternalId = gw2spidyItem.GW2DBExternalId;
                        item.IsRich = true;
                    }
                }
                catch (Exception e)
                {
                    gw2spidyException = true;
                    if (!_cm.CatchExceptions)
                    {
                        Console.WriteLine("Exception getting item from Gw2Spidy: {0}", e.Message);
                    }
                }

                item = add_GW2DB_data(item);

                //gw2dbItem gw2dbItem = this.GetGW2DBItem(item.Id);
                //if (gw2dbItem != null)
                //{
                //    item.Defense = gw2dbItem.Defense;
                //    item.MinPower = gw2dbItem.MinPower;
                //    item.MaxPower = gw2dbItem.MaxPower;
                //    item.ArmorWeightType = gw2dbItem.ArmorWeightType;
                //    item.Stats = gw2dbItem.Stats;
                //    item.SoldBy = gw2dbItem.SoldBy;
                //    item.Recipes = gw2dbItem.Recipes;
                //    item.Description = StripHTML(gw2dbItem.Description);

                //    if (gw2spidyException)
                //    {
                //        item.TypeId = gw2dbItem.TypeId;
                //        item.SubTypeId = gw2dbItem.SubTypeId;
                //        item.GW2DBExternalId = gw2dbItem.GW2DBExternalId;
                //        item.IsRich = ((item.TypeId != TypeEnum.All) && (item.SubTypeId != -1));
                //    }
                //    item.HasGW2DBData = true;
                //}
            }

            return item;
        }

        public Item add_GW2DB_data(Item item)
        {
            if (!item.HasGW2DBData)
            {
                gw2dbItem gw2dbItem = this.GetGW2DBItem(item.Id);
                if (gw2dbItem != null)
                {
                    item.Defense = gw2dbItem.Defense;
                    item.MinPower = gw2dbItem.MinPower;
                    item.MaxPower = gw2dbItem.MaxPower;
                    item.ArmorWeightType = gw2dbItem.ArmorWeightType;
                    item.Stats = gw2dbItem.Stats;
                    item.SoldBy = gw2dbItem.SoldBy;
                    item.Recipes = gw2dbItem.Recipes;
                    item.Description = StripHTML(gw2dbItem.Description);
                    item.TypeId = gw2dbItem.TypeId;
                    item.SubTypeId = gw2dbItem.SubTypeId;
                    item.GW2DBExternalId = gw2dbItem.GW2DBExternalId;
                    item.HasGW2DBData = true;
                }

                gw2apiItem apiItem = this.GetGW2APIItem(item.Id);
                if (apiItem != null)
                {
                    item.TypeId = apiItem.TypeId;
                    switch (item.TypeId)
                    {
                        case TypeEnum.Weapon:
                            item.SubTypeId = (int) apiItem.Weapon.SubTypeId;
                            item.MinPower = apiItem.Weapon.MinPower;
                            item.MaxPower = apiItem.Weapon.MaxPower;
                            item.Defense = apiItem.Weapon.Defense;
                            item.UpgradeId = apiItem.Weapon.UpgradeId;
                            item.DamageType = apiItem.Weapon.DamageType;
                            if (apiItem.Weapon.InfixUpgrade != null) item.Stats = GW2APIInfixUpgradeAttributeToGW2DBStats(apiItem.Weapon.InfixUpgrade.Attributes);
                            break;
                        
                        case TypeEnum.Upgrade_Component:
                            item.SubTypeId = (int) apiItem.UpgradeComponent.SubTypeId;
                            if (apiItem.UpgradeComponent.InfixUpgrade != null) item.Stats = GW2APIInfixUpgradeAttributeToGW2DBStats(apiItem.UpgradeComponent.InfixUpgrade.Attributes);
                            break;

                        case TypeEnum.Trinket:
                            item.SubTypeId = (int) apiItem.Trinket.SubTypeId;
                            item.UpgradeId = apiItem.Trinket.UpgradeId;
                            if (apiItem.Trinket.InfixUpgrade != null) item.Stats = GW2APIInfixUpgradeAttributeToGW2DBStats(apiItem.Trinket.InfixUpgrade.Attributes);
                            break;

                        case TypeEnum.Tool:
                            item.SubTypeId = (int) apiItem.Tool.SubTypeId;
                            break;

                        case TypeEnum.Gizmo:
                            item.SubTypeId = (int) apiItem.Gizmo.SubTypeId;
                            break;

                        case TypeEnum.Gathering:
                            item.SubTypeId = (int) apiItem.Gathering.SubTypeId;
                            break;

                        case TypeEnum.Container:
                            item.SubTypeId = (int) apiItem.Container.SubTypeId;
                            break;

                        case TypeEnum.Consumable:
                            item.SubTypeId = (int) apiItem.Consumable.SubTypeId;
                            break;

                        case TypeEnum.Armor:
                            item.SubTypeId = (int) apiItem.Armor.SubTypeId;
                            item.ArmorWeightType = apiItem.Armor.ArmorWeightType;
                            item.Defense = apiItem.Armor.Defense;
                            item.UpgradeId = apiItem.Armor.UpgradeId;
                            if (apiItem.Armor.InfixUpgrade != null) item.Stats = GW2APIInfixUpgradeAttributeToGW2DBStats(apiItem.Armor.InfixUpgrade.Attributes);
                            break;

                        case TypeEnum.Back:
                            item.UpgradeId = apiItem.Back.UpgradeId;
                            if (apiItem.Back.InfixUpgrade != null) item.Stats = GW2APIInfixUpgradeAttributeToGW2DBStats(apiItem.Back.InfixUpgrade.Attributes);
                            break;
                    }

                    if (item.UpgradeId != null)
                    {
                        item.UpgradeName = GetUpgradeName((int)item.UpgradeId);
                        item.UpgradeDescription = GetUpgradeDescription((int)item.UpgradeId);
                    }

                    item.GameType = apiItem.GameType;
                    item.Flags = apiItem.Flags;
                    item.Restrictions = apiItem.Restrictions;
                    if (string.IsNullOrEmpty(item.Description))
                    {
                        item.Description = apiItem.Description;
                    }

                    item.HasGW2DBData = true;
                }
            }

            return item;
        }

        public async Task<List<Item>> get_my_sells(bool allPages = true, bool past = false, int offset = 1, int count = 10)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                return await get_my_buys_sells(false, allPages, past, offset, count);
            }
            return new List<Item>();
        }

        public async Task<List<Item>> get_my_buys(bool allPages = true, bool past = false, int offset = 1, int count = 10)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                //using (var redisClient = new RedisClient("localhost", 6379, "thhedh11"))
                //{
                //    IRedisTypedClient<Item> redis = redisClient.As<Item>();

                //    IRedisList<Item> oldBoughtList = redis.Lists["urn:GW2Miner:Engine:BoughtList"];                    
                //}
                return await get_my_buys_sells(true, allPages, past, offset, count);
            }
            return new List<Item>();
        }

        public async Task<List<Item>> get_my_buys_sells_transactions(bool buy, bool allPages = true, bool past = false, int offset = 1, int count = 10)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                return await get_my_buys_sells(buy, allPages, past, offset, count);
            }
            return new List<Item>();
        }

        public async Task<List<ItemBuySellListingItem>> get_buy_listings(int item_id)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                return await get_buy_sell_listings(true, item_id);
            }
            return new List<ItemBuySellListingItem>();
        }

        public async Task<List<ItemBuySellListingItem>> get_sell_listings(int item_id)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                return await get_buy_sell_listings(false, item_id);
            }
            return new List<ItemBuySellListingItem>();
        }

        public async Task<List<ItemBuySellListingItem>> get_buy_sell_item_listings(int item_id, bool buy)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                return await get_buy_sell_listings(buy, item_id);
            }
            return new List<ItemBuySellListingItem>();
        }

        public async void cancelBuyOrder(int item_id, long listing_id)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                await _cm.CancelBuySellListing(item_id, listing_id, true, false);
            }
        }

        public async void cancelSellOrder(int item_id, long listing_id)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                await _cm.CancelBuySellListing(item_id, listing_id, false, false);
            }
        }

        public async void Buy(int item_id, int count, int price)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                await _cm.Buy(item_id, count, price, false);
            }
        }

        public void RenewBuyOrder(Item item, int price)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                cancelBuyOrder(item.Id, item.ListingId);
                Buy(item.Id, item.Quantity, price);
            }
        }

        // assumes sellList is sorted from lowest to highest sell prices
        public void BuyAllRidiculousSellOrders(List<ItemBuySellListingItem> sellList, Item item)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                foreach (ItemBuySellListingItem listing in sellList)
                {
                    if (item.UnitPrice * 0.85 > listing.PricePerUnit)
                    {
                        Buy(item.Id, listing.NumberAvailable, listing.PricePerUnit);
                    }
                    else break;
                }
            }
        }

        public async Task<GemPriceTP> get_gem_price()
        {
            if (!TradeWorker.gettingSessionKey)
            {
                GemPriceTP retGemPrices = new GemPriceTP();
                Stream gemPriceStreams = await _cm.RequestGemPrice(10000000, 100000);

                GemPriceParser goldGemPriceParser = new GemPriceParser();

                GemPriceList2 gemPrice = goldGemPriceParser.Parse(gemPriceStreams);

                retGemPrices.gem_to_gold = gemPrice.GemPrice4GemsToGold.quantity / 1000;
                retGemPrices.gold_to_gem = (1000000000 / gemPrice.GemPrice4GoldToGems.quantity);

                return retGemPrices;
            }
            return new GemPriceTP();
        }

        public double BlackLionKitSalvageCost
        {
            get
            {
                if (!TradeWorker.gettingSessionKey)
                {
                    Task<TradeWorker.GemPriceTP> gemPrice = this.get_gem_price();
                    int hundredGemPrice = ((TradeWorker.GemPriceTP)(gemPrice.Result)).gold_to_gem;
                    return hundredGemPrice * 3.0 / 25.0;
                }
                return 0.0;
            }
        }

        public double MasterKitSalvageCost
        {
            get
            {
                return 61.44;
            }
        }

        public event EventHandler FnLoginInstructions
        {
            add
            {
                lock (_fnCallGW2LoginInstructionsLock)
                {
                    _fnCallGW2LoginInstructions += value;
                    _cm.FnLoginInstructions -= OnCMFnLoginInstructions;
                    _cm.FnLoginInstructions += OnCMFnLoginInstructions;
                    _cm.CatchExceptions = false;
                }
            }

            remove
            {
                lock (_fnCallGW2LoginInstructionsLock)
                {
                    _fnCallGW2LoginInstructions -= value;
                    _cm.FnLoginInstructions -= OnCMFnLoginInstructions;
                }
            }
        }

        public event EventHandler FnGW2Logined
        {
            add
            {
                lock (_fnGW2LoginedLocked)
                {
                    _fnGW2Logined += value;
                    _cm.FnGW2Logined -= OnCMFnGW2Logined;
                    _cm.FnGW2Logined += OnCMFnGW2Logined;
                    _cm.CatchExceptions = false;
                }
            }

            remove
            {
                lock (_fnGW2LoginedLocked)
                {
                    _fnGW2Logined -= value;
                    _cm.FnGW2Logined -= OnCMFnGW2Logined;
                }
            }
        }

        // suffix: e.g. "of Fire"
        // data_id: Id of upgrade
        // itemName: name of the item (e.g. "Kodanroar" or "Berserker shortbow of Fire")
        public bool Match(string suffix, int data_id, string itemName)
        {
            List<string> collection1;

            switch (data_id)
            {
                ////////////////// Sigils //////////////////////////////

                case 24548: // Superior Sigil of Fire
                    collection1 = new List<string> { "^Al'ir'aska$", "^Mjolnir$", "^Titan's Vengance$", "^Volcanus$", "^Magmaton$", "^Mystic Battlehammer$" };
                    break;
                case 24551: // Superior Sigil of Water
                    collection1 = new List<string> { "^Azure Railgun$", "^Droknar's Short Bow$", "^Nitro$", "^Droknar's Recurve Bow$", "^Arcanus Obscurus$", "^Ambrosia$" };
                    break;
                case 24554: // Superior Sigil of Air
                    collection1 = new List<string> { "^Moonshine$", "^Peasant's Solution$", "^Bite of the Ebon Viper$", "^Khrysaor the Golden Sword$", "^Crystalline Blade$" };
                    break;
                case 24555: // Superior Sigil of Ice
                    collection1 = new List<string> { "^The Maelstrom$", "^Naga Fang$", "^Eir's Longbow$", "^Jormag's Breath$", "^Wintersbite$" };
                    break;
                case 24560: // Superior Sigil of Earth
                    collection1 = new List<string> { "^Berserker's Mace$", "^Melandru's Gaze$", "^Adder's Hiss$", "^Axiquiotl$", "^Serpentsniper$", "^Éibhear Finn$", "^Final Curse$", "^Oikoumene$", "^Ironfist$" };
                    break;
                case 24561: // Superior Sigil of Rage
                    collection1 = new List<string> { "^Gearbore$", "^Dhuumseal$", "^Alderune's Last Stand$", "^Hypnotic Scepter$", "^Kryta's Embrace$", "^Limitless Furnace$", "^Venom$", "^Glint's Scale$", "^Rodgort's Flame$", "^Carcharias$", "^The Bard$", "^Rage$", "^Howl$", "^The Energizer$", "^Storm$", "^Spirit Links$", "^Chaos Gun$", "^Abyssal Scepter$", "^Leaf of Kudzu$", "^The Chosen$", "^Tooth of Frostfang$", "^The Hunter$", "^Zap$", "^The Lover$", "^Spark$", "^Dawn$", "^The Colossus$", "^The Legend$", "^Dusk$" };
                    break;
                case 24570: // Superior Sigil of Blood
                    collection1 = new List<string> { "^Coiler$", "^Eye of Rodgort$", "^Lidless Eye$", "^The Fate of Menzies$", "^Unspoken Curse$", "^Super Hyperbeam Alpha$", "^Malefacterym$", "^Deathwish$", "^Mirage$", "^Mystic Caller$", "^Mystic Cudgel$", "^Mystic Rifle$", "^Mystic Speargun$", "^Mystic Torch$", "^Mystic Trident$", "^Mystic Wand$" };
                    break;
                case 24571: // Superior Sigil of Purity
                    collection1 = new List<string> { "^Sarraceinaceae$", "^Shield of the Moon$", "^Azureflame$", "^Aether$", "^Glimmerfang$", "^Serpentstone$", "^Mystic Crescent$", "^Mystic Hornbow$" };
                    break;
                case 24572: // Superior Sigil of Nullification
                    collection1 = new List<string> { "^Grimward$", "^Eidolon$", "^Soulshard$", "^Naegling$", "^Wall of the Mists$", "^Reaver of the Mists$", "^Vision of the Mists$", "^The Anomaly$", "^Lyss$", "^Mystic Barricade$", "^Mystic Claymore$", "^Mystic Pistol$", "^Mystic Staff$" };
                    break;
                case 24575: // Superior Sigil of Bloodlust
                    collection1 = new List<string> { "^The Stingray$", "^Phoenix Talon$", "^The Ugly Stick$", "^Tsunami$", "^Avirdanag$", "^Honor of Humanity$", "^Mystic Battleaxe$" };
                    break;
                case 24578: // Superior Sigil of Corruption
                    collection1 = new List<string> { "^Atlatl$", "^Ganadriva$", "^Delusion$", "^Kymswarden$", "^Winged Spatha$", "^Jaws of Death$" };
                    break;
                case 24580: // Superior Sigil of Perception
                    collection1 = new List<string> { "^Coldsnap$", "^Kryta's Salvation$" };
                    break;
                case 24582: // Superior Sigil of Life
                    collection1 = new List<string> { "^Siren's Call$", "^Foefire's Essence$", "^Remnant of Ascalon$", "^Resonator$", "^Skybringer$" };
                    break;
                case 24583: // Superior Sigil of Demon Summoning
                    collection1 = new List<string> { "^Bloodseeker$", "^Gaze$", "^Stygian Blade$" };
                    break;
                case 24591: // Superior Sigil of Luck
                    collection1 = new List<string> { "^Infinite Light$" };
                    break;
                case 24592: // Superior Sigil of Stamina
                    collection1 = new List<string> { "^Rivetwall$", "^Vera$", "^X7-10 Alpha$", "^Rhongomyniad$", "^Goblet of Kings$", "^Immobulus$" };
                    break;
                case 24594: // Superior Sigil of Restoration
                    collection1 = new List<string> { "^Firelighter$", "^Master Blaster$", "^Drakevenom$", "^Knot of Justice$", "^Memory of the Sky$" };
                    break;
                case 24597: // Superior Sigil of Hydromancy
                    collection1 = new List<string> { "^Sun God's Gift$", "^Horn of the Rogue Bull$", "^Malachidean$", "^The Punisher$", "^Wintersbark$", "^Tear of Grenth$", "^Jormag's Needle$", "^Mystic Spike$" };
                    break;
                case 24599: // Superior Sigil of Leeching
                    collection1 = new List<string> { "^Scorchrazor's Fist$", "^Mojo$", "^Drakestrike$", "^Ak-Muhl's Jaw$", "^Anura$" };
                    break;
                case 24600: // Superior Sigil of Intelligence
                    collection1 = new List<string> { "^Beacon of the True Legions$", "^Éibhear Dunn$", "^Adam$", "^Brandt$", "^Guardian of the Six$" };
                    break;
                case 24601: // Superior Sigil of Battle
                    collection1 = new List<string> { "^Cooguloosh$", "^Claws of the Desert$", "^Shield of the Wing$", "^Kodanroar$", "^Ebonblade$" };
                    break;
                case 24605: // Superior Sigil of Geomancy
                    collection1 = new List<string> { "^The Hunt$", "^Infinite Wisdom$", "^Handheld Disaster$", "^Emberglow$", "^Song of the Numberless Pack$", "^Heart of Mellaggan$", "^Flux Matrix$", "^Mystic Artifact$" };
                    break;
                case 24607: // Superior Sigil of Energy
                    collection1 = new List<string> { "^Tinwail$", "^Defiant Blaze$", "^Chalice of the Gods$", "^Fixer Upper$", "^Courage$" };
                    break;
                case 24612: // Superior Sigil of Agony
                    collection1 = new List<string> { "^Droknar's Forgehammer$", "^Urchin's Needles$" };
                    break;
                case 24615: // Superior Sigil of Force
                    collection1 = new List<string> { "^Black Fleet Bludgeon$", "^Trident of the True Legion$", "^The Briny Deep$", "^Rusttooth$", "^Bow of the Pale Stag$", "^Faithful$", "^Imryldyeen$", "^Big Juju$", "^Maw of the Damned$", "^Twin Sisters$", "^Mystic Sword$" };
                    break;
                case 24618: // Superior Sigil of Accuracy
                    collection1 = new List<string> { "^Blaze of the Serpents$", "^Accursed Chains$", "^Windstorm$", "^Flamebelcher$", "^Spade of the Deep$", "^Combustion$", "^Bow of the White Hart$", "^Eir's Short Bow$", "^Ruinmaker$", "^Moonshank$", "^Usoku's Needle$", "^Charrzooka$" };
                    break;
                case 24621: // Superior Sigil of Peril
                    collection1 = new List<string> { "^Breath of Flame$", "^Foefire's Power$", "^Kenshi's Wing$", "^Ilya$", "^Whisperblade$", "^Illusion$", "^Ignus Fatuus$", "^Mystic Spear$" };
                    break;
                case 24624: // Superior Sigil of Smoldering
                    collection1 = new List<string> { "^Dragonfury$", "^Emberspire$", "^Venomstriker$", "^Firebringer$" };
                    break;
                case 24627: // Superior Sigil of Hobbling
                    collection1 = new List<string> { "^Beacon of Kryta$", "^Silence$", "^Labrys$", "^Dreadwing$" };
                    break;
                case 24630: // Superior Sigil of Chilling
                    collection1 = new List<string> { "^Spectral Wave Modulator$", "^Dragonshot$", "^Bramblethorne$", "^Godswalk Enchiridion$" };
                    break;
                case 24632: // Superior Sigil of Venom
                    collection1 = new List<string> { "^Dragonspine$", "^Ophidian$", "^Trosa's Short Bow$", "^Squeedily Spooch$" };
                    break;
                case 24636: // Superior Sigil of Debility
                    collection1 = new List<string> { "^Razorstone$", "^Jora's Defender$", "^Malice$", "^Cragstone$" };
                    break;
                case 24648: // Superior Sigil of Grawl Slaying
                    collection1 = new List<string> { "^Kevin$" };
                    break;
                case 24658: // Superior Sigil of Serpent Slaying
                    collection1 = new List<string> { "^Steamfire$", "^Gungnir$", "^Blastmaster 3000$" };
                    break;
                case 36053: // Superior Sigil of the Night
                    collection1 = new List<string> { "^The Mad Moon$", "^Arachnophobia$", "^The Crossing$" };
                    break;

                ////////////////// Runes //////////////////////////////

                case 24687: // Superior Rune of the Afflicted
                    collection1 = new List<string> { "^Brutus", "^Sheena" };
                    break;
                case 24688: // Superior Rune of the Lich
                    collection1 = new List<string> { "^Khilbron" };
                    break;
                case 24691: // Superior Rune of the Traveler
                    collection1 = new List<string> { "^Yakkington" };
                    break;
                case 24696: // Superior Rune of the Flock
                    collection1 = new List<string> { "^Zho" };
                    break;
                case 24699: // Superior Rune of the Dolyak
                    collection1 = new List<string> { "^Jalis" };
                    break;
                case 24702: // Superior Rune of the Pack
                    collection1 = new List<string> { "^Aidan" };
                    break;
                case 24703: // Superior Rune of Infiltration
                    collection1 = new List<string> { "^Nika" };
                    break;
                case 24708: // Superior Rune of Mercy
                    collection1 = new List<string> { "^Tahlkora" };
                    break;
                case 24711: // Superior Rune of Vampirism
                    collection1 = new List<string> { "^Rurik" };
                    break;
                case 24714: // Superior Rune of Strength
                    collection1 = new List<string> { "^Devona", "^Ogden" };
                    break;
                case 24717: // Superior Rune of Rage
                    collection1 = new List<string> { "^Shiro" };
                    break;
                case 24723: // Superior Rune of the Eagle
                    collection1 = new List<string> { "^Errol" };
                    break;
                case 24738: // Superior Rune of Scavenging
                    collection1 = new List<string> { "^Jatoro" };
                    break;
                case 24765: // Superior Rune of Balthazar
                    collection1 = new List<string> { "^Norgu", "^Koss" };
                    break;
                case 24768: // Superior Rune of Dwayna
                    collection1 = new List<string> { "^Mhenlo" };
                    break;
                case 24771: // Superior Rune of Melandru
                    collection1 = new List<string> { "^Reyna" };
                    break;
                case 24779: // Superior Rune of Grenth
                    collection1 = new List<string> { "^Galrath" };
                    break;
                case 24788: // Superior Rune of the Centaur
                    collection1 = new List<string> { "^Zhed" };
                    break;
                case 24797: // Superior Rune of Flame Legion
                    collection1 = new List<string> { "^Vatlaaw" };
                    break;
                default:
                    collection1 = new List<string> { };
                    break;

            }

            collection1.Add(suffix + "$");

            foreach (string str in collection1)
            {
                //if (!itemName.match(collection[i])) continue;
                if (!Regex.Match(itemName, str).Success) continue;

                return true;
            }
            return false;
        }

        public Item get_upgrade(Item item, List<Item> upgradesCollection)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                if (item == null) return null;

                int upgradeItemId = GW2APIGetItemUpgrade(item.Id);

                if (upgradeItemId != 0)
                {
                    List<Item> upgrades = this.get_items(upgradeItemId).Result;
                    return upgrades[0];
                }

                if (!item.IsRich)
                {
                    //List<Item> richItems = get_rich_items(item.Id).Result;
                    //item = richItems[0];
                    //if (!item.IsRich) return null;
                    item = make_rich_item(item).Result;
                }

                // TODO: support trinkets and crests/jewels
                if (item.TypeId != TypeEnum.Armor && item.TypeId != TypeEnum.Weapon) return null;

                return FindUpgrade(item, upgradesCollection);
            }
            return null;
        }

        public int Worth(Item item, List<Item> sellItemList = null, List<Item> upgradesCollection = null)
        {
            bool iAmSelling, underCut;
            Item myItemOnSale;

            if (!TradeWorker.gettingSessionKey)
            {
                if (sellItemList == null) sellItemList = get_my_sells().Result;

                int mySellPrice = GetMySellPrice(sellItemList, item, out iAmSelling, out underCut, out myItemOnSale);
                item.IAmSelling = iAmSelling;
                item.myItemOnSale = myItemOnSale;

                Item upgrade = get_upgrade(item, upgradesCollection);
                if (upgrade == null)
                {
                    return Math.Max(item.VendorPrice, (int)(0.85 * mySellPrice));
                }
                else
                {
                    double BLSalvageCost = BlackLionKitSalvageCost;
                    return Math.Max(Math.Max(item.VendorPrice, (int)(0.85 * mySellPrice)),
                                     Math.Max((int)(0.85 * GetMySellPrice(sellItemList, upgrade, out iAmSelling, out underCut, out myItemOnSale) - BLSalvageCost), upgrade.VendorPrice - (int)BLSalvageCost));
                }
            }
            return -1;
        }

        public int GetMySellPrice(List<Item> sellList, Item item, out bool iAmSelling, out bool underCut, out Item myItemOnSale)
        {
            myItemOnSale = null;
            iAmSelling = true; // assume that we are selling this item first
            underCut = false; // assume that we are not being undercut first
            if (!TradeWorker.gettingSessionKey)
            {
                for (int i = 0; i < sellList.Count; i++)
                {
                    Item itemInMySellList = sellList[i];
                    if (itemInMySellList.Id == item.Id)
                    {
                        myItemOnSale = itemInMySellList;
                        Task<List<ItemBuySellListingItem>> itemSellListing = get_sell_listings(item.Id);
                        if (itemSellListing.Result != null && itemSellListing.Result.Count > 0)
                        {
                            ItemBuySellListingItem listing = itemSellListing.Result[0];
                            // we are selling this item
                            if (listing.PricePerUnit >= itemInMySellList.UnitPrice)
                            {
                                // we are at the min sale price
                                return item.MinSaleUnitPrice;
                            }
                            else
                            {
                                // we are being undercut
                                underCut = true;
                                return (item.MinSaleUnitPrice - 1);
                            }
                        }
                    }
                }
                // this item is not in our sell list yet
                iAmSelling = false;
                return (item.MinSaleUnitPrice - 1);
            }

            return 0;
        }

        // assumes list sorted from most recent to least
        public int GetMyBoughtPrice(Item transItem, DateTime dateTimeSold, List<Item> itemBoughtList = null)
        {
            if (!TradeWorker.gettingSessionKey)
            {
                if (itemBoughtList == null)
                    itemBoughtList = get_my_buys(true, true).Result;

                //if (!transItem.IsRich)
                //{
                //    transItem = make_rich_item(transItem).Result;
                //}

                foreach (Item item in itemBoughtList)
                {
                    if (item.Purchased > dateTimeSold) continue; // must have bought before sold, not after

                    int upgradeItemId = GW2APIGetItemUpgrade(item.Id);
                    if (transItem.Id == upgradeItemId)
                    {
                        return item.UnitPrice;
                    }

                    //if (transItem.IsRich && transItem.TypeId == TypeEnum.UpgradeComponent)
                    //{
                    //    Item upgrade = get_upgrade(item);
                    //    if (upgrade != null && transItem.Id == upgrade.Id)
                    //        return item.UnitPrice;
                    //}

                    if (item.Id == transItem.Id)
                    {
                        return item.UnitPrice;
                    }
                }
            }

            return 0;
        }

        public Args LastSearchArgs
        {
            get
            {
                return this.searchArgs;
            }
        }

        public Args LastTransactionArgs
        {
            get
            {
                return this.transArgs;
            }
        }

        public void WaitForGW2DBLoaded()
        {
            while (!gw2dbLoaded)
            {
                Thread.Sleep(500);
            }
        }

        public void WaitForGW2APILoaded()
        {
            while (!gw2apiLoaded)
            {
                Thread.Sleep(500);
            }
        }

        public async Task<gw2spidyRecipe> get_gw2spidy_recipe(int recipeId)
        {
            Stream recipeStream = await _sm.RequestGw2spidyRecipe(recipeId);

            gw2spidyOneRecipeParser recipeParser = new gw2spidyOneRecipeParser();
            gw2spidyRecipeResult recipeResult = recipeParser.Parse(recipeStream);
            return recipeResult.Recipe;
        }

        public async Task<List<gw2spidyRecipe>> get_gw2spidy_recipes(GW2DBDisciplines discipline)
        {
            List<gw2spidyRecipe> allRecipeList = new List<gw2spidyRecipe>();
            gw2spidyRecipeListParser recipeListParser = new gw2spidyRecipeListParser();
            int currentPage = 1;

            while (true)
            {
                try
                {
                    Stream recipesStream = await _sm.RequestGw2spidyAllRecipes(discipline, currentPage);
                    gw2spidyRecipeList recipeList = recipeListParser.Parse(recipesStream);
                    allRecipeList.AddRange(recipeList.Recipes);
                    currentPage++;
                    if (recipeList.Page == recipeList.LastPage)
                    {
                        break;
                    }
                }
                catch(Exception e)
                {
                    break;
                }
            }

            return allRecipeList;
        }

        private string GetUpgradeName(int upgradeId)
        {
            string upgradeName = "";
            gw2apiItem upgrade = GetGW2APIItem(upgradeId);
            if (upgrade != null && upgrade.TypeId == TypeEnum.Upgrade_Component)
            {
                upgradeName = upgrade.Name;
            }
            return upgradeName;
        }

        private string GetUpgradeDescription(int upgradeId)
        {
            string upgradeDescription = "";
            gw2apiItem upgrade = GetGW2APIItem(upgradeId);
            if (upgrade != null && upgrade.TypeId == TypeEnum.Upgrade_Component)
            {
                if (upgrade.UpgradeComponent.Bonuses != null && upgrade.UpgradeComponent.Bonuses.Count > 0)
                {
                    int i = 1;
                    foreach (string bonus in upgrade.UpgradeComponent.Bonuses)
                    {
                        upgradeDescription += string.Format("({0}):{1}{2}", i, StripHTML(bonus), (i == upgrade.UpgradeComponent.Bonuses.Count) ? "" : "\n");
                        i++;
                    }
                }
                else if (upgrade.UpgradeComponent.InfixUpgrade.Buff != null)
                {
                    upgradeDescription = StripHTML(upgrade.UpgradeComponent.InfixUpgrade.Buff.Description);
                }
                else
                {
                    gw2dbItem dbItem = GetGW2DBItem(upgradeId);
                    if (dbItem != null) upgradeDescription = StripHTML(dbItem.Description);
                }
            }
            return upgradeDescription;
        }

        private List<gw2dbStat> GW2APIInfixUpgradeAttributeToGW2DBStats(List<gw2apiInfixUpgradeAttribute> stats)
        {
            List<gw2dbStat> retAttributes = new List<gw2dbStat>();
            foreach (gw2apiInfixUpgradeAttribute stat in stats)
            {
                gw2dbStat attribute = new gw2dbStat();
                attribute.Type = (GW2DBItemStats)Enum.ToObject(typeof(GW2DBItemStats), stat.Attribute);
                attribute.Value = (float)stat.Modifier;
                retAttributes.Add(attribute);
            }

            return retAttributes;
        }

        private static Dictionary<int, gw2dbRecipe> CreatedIdToRecipe
        {
            get { return createdIdToRecipe; }
            set
            {
                lock (_CreatedIdToRecipeLocked)
                {
                    createdIdToRecipe = value;
                }
            }
        }

        private async Task<gw2spidyItem> get_gw2spidy_item(int itemId)
        {
            Stream itemStream = await _sm.RequestGw2spidyItem(itemId);

            gw2spidyOneItemParser itemParser = new gw2spidyOneItemParser();
            gw2spidyItemResult itemResult = itemParser.Parse(itemStream);
            return itemResult.Item;
        }

        private Item FindUpgrade(Item item, List<Item> upgradesCollection)
        {
            int upgradeSubtype = (int)(item.TypeId == TypeEnum.Armor ? UpgradeComponentSubTypeEnum.Rune : UpgradeComponentSubTypeEnum.Sigil);

            if (upgradesCollection == null) upgradesCollection = this.search_items("", true, TypeEnum.Upgrade_Component, upgradeSubtype).Result;

            foreach (Item upgrade in upgradesCollection)
            {
                if (item.RarityId != upgrade.RarityId) continue;

                string name = upgrade.Name;

                if (upgrade.BuyCount <= 0 || (name.IndexOf("Rune") < 0 && name.IndexOf("Sigil") < 0)) continue;

                string[] words = name.Split(' ');
                string[] wordsTransformed = new string[words.Length - 2];
                Array.Copy(words, 2, wordsTransformed, 0, wordsTransformed.Length);
                string suffix = string.Join(" ", wordsTransformed);

                if (Match(suffix, upgrade.Id, item.Name)) return upgrade;
            }

            return null;
        }

        private void OnCMFnLoginInstructions(object sender, EventArgs e)
        {
            gettingSessionKey = true;
            if (_fnCallGW2LoginInstructions != null) _fnCallGW2LoginInstructions(this, e);
        }

        private void OnCMFnGW2Logined(object sender, EventArgs e)
        {
            gettingSessionKey = false;
            if (_fnGW2Logined != null) _fnGW2Logined(this, e);
        }

        private void UpdateArgs(ref Args args, int offset, int count, int total)
        {
            if (offset > 0) args.offset = offset;
            args.count = count;
            args.max = total;
        }

        private async Task<List<Item>> Search(string query, bool allPages = true, int origOffset = 1, int requested = 10)
        {
            Stream itemStreams = await _cm.RequestItems("search", String.Concat(query, String.Format("&offset={0}&count={1}", origOffset, requested)), false);

            ItemParser itemParser = new ItemParser();
            ItemList allItemList;
            List<Item> retItemList;
            int count, listSize;
            lock (classLock)
            {
                allItemList = itemParser.Parse(itemStreams);
                retItemList = allItemList.Items;

                if (allItemList.args != null) UpdateArgs(ref this.searchArgs, allItemList.args.Offset, retItemList.Count, allItemList.Total); // Record the last search max count

                count = (allPages ? allItemList.Total : Math.Min(requested, allItemList.Total));
                listSize = retItemList.Count;
            }

            //int listCount = allItemList.Total;
            while ((count - origOffset + 1) > listSize)
            {
                int offset = listSize + origOffset;
                itemStreams = await _cm.RequestItems("search", String.Concat(query, String.Format("&offset={0}&count={1}", offset, count - listSize - origOffset + 1)), false);
                lock (classLock)
                {
                    ItemList itemList = itemParser.Parse(itemStreams);
                    UpdateArgs(ref this.searchArgs, -1, this.searchArgs.count + itemList.Items.Count, allItemList.Total); // Record the last search max count
                    count = (allPages ? itemList.Total : Math.Min(requested, itemList.Total));
                    retItemList.AddRange(itemList.Items);
                    listSize = retItemList.Count;
                    //listCount = itemList.Total;
                }
            }

            return retItemList;
        }

        private async Task<List<Item>> Items(string query)
        {
            Stream itemStreams = await _cm.RequestItems("items", query, false);

            ItemParser itemParser = new ItemParser();
            ItemList allItemList;
            List<Item> retItemList;
            int count, listSize;
            lock (classLock)
            {
                allItemList = itemParser.Parse(itemStreams);
                retItemList = allItemList.Items;

                if (allItemList.args != null) UpdateArgs(ref this.searchArgs, allItemList.args.Offset, retItemList.Count, allItemList.Total); // Record the last search max count

                count = allItemList.Total;
                listSize = retItemList.Count;
            }

            //int listCount = allItemList.Total;
            while (count > listSize)
            {
                int offset = listSize + 1;
                itemStreams = await _cm.RequestItems("items", String.Concat(query, String.Format("&offset={0}&count={1}", offset, count - listSize)), false);
                lock (classLock)
                {
                    ItemList itemList = itemParser.Parse(itemStreams);
                    UpdateArgs(ref this.searchArgs, -1, this.searchArgs.count + itemList.Items.Count, allItemList.Total); // Record the last search max count
                    count = itemList.Total;
                    retItemList.AddRange(itemList.Items);
                    listSize = retItemList.Count;
                    //listCount = itemList.Total;
                }
            }

            return retItemList;
        }

        private async Task<List<Item>> get_my_buys_sells(bool buy, bool allPages = true, bool past = false, int origOffset = 1, int count = 10)
        {
            Stream itemStreams = await _cm.RequestMyBuysSells(buy, false, origOffset, past, count);

            ItemListParser itemParser = new ItemListParser();
            MyItemList allItemList;
            List<Item> retItemList;
            lock (classLock)
            {
                allItemList = itemParser.Parse(itemStreams);
                retItemList = allItemList.Items;

                if (allItemList.args != null) UpdateArgs(ref this.transArgs, allItemList.args.Offset, allItemList.args.Count, allItemList.Total); // Record the last search max count
            }

            if (allPages)
            {
                int listCount = allItemList.Total;
                int listSize = retItemList.Count;

                while ((listCount - origOffset + 1) > listSize)
                {
                    int offset = listSize + origOffset;
                    // TODO: If past == true, attempt to read from saved JSON file instead first
                    itemStreams = await _cm.RequestMyBuysSells(buy, false, offset, past, listCount - listSize - origOffset + 1);
                    lock (classLock)
                    {
                        MyItemList itemList = itemParser.Parse(itemStreams);
                        UpdateArgs(ref this.transArgs, -1, this.transArgs.count + itemList.Items.Count, allItemList.Total); // Record the last search max count
                        listCount = itemList.Total;
                        retItemList.AddRange(itemList.Items);
                        listSize = retItemList.Count;
                    }
                }
            }

            return retItemList;
        }

        private async Task<List<ItemBuySellListingItem>> get_buy_sell_listings(bool buy, int item_id)
        {
            Stream ListingStreams = await _cm.RequestBuySellListing(item_id, buy, false);

            ListingParser listingParser = new ListingParser();
            return listingParser.Parse(ListingStreams, buy);
        }

        const string HTML_TAG_PATTERN = "<.*?>";
        const string HTML_BREAK_TAG_PATTERN = @"<br\s*/?>";

        private static string StripHTML(string inputString)
        {
            if (inputString == null) return String.Empty;

            string retString = Regex.Replace
              (inputString, HTML_BREAK_TAG_PATTERN, "\n", RegexOptions.IgnoreCase);

            retString = Regex.Replace
              (retString, HTML_TAG_PATTERN, string.Empty);

            return retString;
         }

        private double RecipeUpdatedTimeSpanInMinutes
        {
            get
            {
                int recipeUpdatedTimeSpan = 15; // defaults to 15 mins
                if (ConnectionManager._config.AppSettings.Settings["RecipeUpdatedTimeSpanInMinutes"] == null || 
                        !Int32.TryParse(ConnectionManager._config.AppSettings.Settings["RecipeUpdatedTimeSpanInMinutes"].Value, out recipeUpdatedTimeSpan))
                {
                    ConnectionManager._config.AppSettings.Settings.Add("RecipeUpdatedTimeSpanInMinutes", recipeUpdatedTimeSpan.ToString());
                    ConnectionManager._config.Save(ConfigurationSaveMode.Modified);
                }

                return (double)recipeUpdatedTimeSpan;
            }
        }

        public gw2dbItem SearchGW2DBItem(string name)
        {
            List<gw2dbItem> list = dataIdToItem.Values.ToList();
            foreach (gw2dbItem item in list)
            {
                if (string.Compare(item.Name, name, true) == 0) return item;
            }
            return null;
        }

        private string Plural(string name)
        {
            if (string.Compare("Cherry", name, true) == 0) return "Cherries";
            else if (string.Compare("Peach", name, true) == 0) return "Peaches";
            else if (string.Compare("Glass of Buttermilk", name, true) == 0) return "Buttermilk";
            else if (string.Compare("Packet of Yeast", name, true) == 0) return "Yeast";
            else if ((string.Compare("Cumin", name, true) == 0) || (string.Compare("Horseradish Root", name, true) == 0)) return name;
            else return name + "s";
        }

        // e.g. "Almond[s]" => "Almonds in Bulk"
        // Hacky, until we can get better data for the conversion
        private gw2dbItem GetIngredientBulk(int dataId)
        {
            gw2dbItem item = GetGW2DBItem(dataId);
            if (item == null) return null;
            string name = item.Name;

            string pattern = Regex.Escape("[") + ".*?]";
            name = Regex.Replace(name, pattern, string.Empty);

            //int lastIndex = name.LastIndexOf('[');
            //if (lastIndex >= 0) name = name.Remove(lastIndex);
            return SearchGW2DBItem(Plural(name) + " in Bulk");
        }

        private float MinAcquisitionSkillPointCost(gw2dbItem item)
        {
            if (item == null)
            {
                return 0.0f;
            }

            float minSkillPoints = float.MaxValue;
            foreach (gw2dbSoldBy soldBy in item.SoldBy)
            {
                if (soldBy.SkillPointCost > 0 && soldBy.SkillPointCost < minSkillPoints) minSkillPoints = soldBy.SkillPointCost;
            }
            if (minSkillPoints == float.MaxValue) minSkillPoints = 0.0f;
            return minSkillPoints;
        }

        private float MinBulkAcquisitionUnitSkillPointCost(int dataId)
        {
            float minSkillPoints;
            minSkillPoints = MinAcquisitionSkillPointCost(GetGW2DBItem(dataId));
            //if (minSkillPoints == 0)
            //{
            //    gw2dbItem item = GetIngredientBulk(dataId);
            //    return MinAcquisitionSkillPointCost(item) / 25;
            //}
            return minSkillPoints;
        }

        private int MinAcquisitionKarmaCost(gw2dbItem item)
        {
            if (item == null)
            {
                return 0;
            }

            int minKarma = int.MaxValue;
            foreach (gw2dbSoldBy soldBy in item.SoldBy)
            {
                if (soldBy.KarmaCost > 0 && soldBy.KarmaCost < minKarma) minKarma = soldBy.KarmaCost;
            }
            if (minKarma == int.MaxValue) minKarma = 0;
            return minKarma;
        }

        private int MinBulkAcquisitionUnitKarmaCost(int dataId)
        {
            int minKarma;
            minKarma = MinAcquisitionKarmaCost(GetGW2DBItem(dataId));
            if (minKarma == 0)
            {
                gw2dbItem item = GetIngredientBulk(dataId);
                return MinAcquisitionKarmaCost(item) / 25;
            }
            return minKarma;
        }

        private int MinAcquisitionGoldCost(gw2dbItem item)
        {
            if (item == null)
            {
                return 0;
            }

            int minGold = int.MaxValue;
            foreach (gw2dbSoldBy soldBy in item.SoldBy)
            {
                if (soldBy.GoldCost > 0 && soldBy.GoldCost < minGold) minGold = soldBy.GoldCost;
            }
            if (minGold == int.MaxValue) minGold = 0;
            return minGold;
        }

        private int MinBulkAcquisitionUnitGoldCost(int dataId)
        {
            int minGold;
            minGold = MinAcquisitionGoldCost(GetGW2DBItem(dataId));
            if (minGold == 0)
            {
                gw2dbItem item = GetIngredientBulk(dataId);
                return MinAcquisitionGoldCost(item) / 25;
            }
            return minGold;
        }
    }
}
