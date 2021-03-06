﻿ using System;
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

using GW2Miner.Domain;

namespace GW2Miner.Engine
{
    /// <summary>
    /// TODO: The strength of gw2spidy is the constant monitoring of prices.  Add routine to calculate the monthly average buy and sell prices for the last month, for an item.
    /// </summary>
    public class Gw2spidyManager
    {
        static readonly String API_VERSION = "v0.9";

        public async Task<Stream> RequestGw2spidyGemPrice()
        {
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/gem-price", API_VERSION);

            return await Request(url);
        }

        public async Task<Stream> RequestGw2spidyItemListings(int itemId, bool buy, int currentPage = 1)
        {
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/listings/{1}/{2}/{3}", API_VERSION, itemId, (buy ? "buy" : "sell"), currentPage);

            return await Request(url);
        }

        public async Task<Stream> RequestGw2spidyItem(int itemId)
        {
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/item/{1}", API_VERSION, itemId);

            return await Request(url);
        }

        public async Task<Stream> RequestGw2spidyFullItemList(int type)
        {
            string typeString = (type < 0) ? "all" : type.ToString();
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/all-items/{1}", API_VERSION, typeString);

            return await Request(url);
        }

        public async Task<Stream> RequestGw2spidyRecipe(int recipeId)
        {
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/recipe/{1}", API_VERSION, recipeId);

            return await Request(url);
        }

        public async Task<Stream> RequestGw2spidyAllRecipes(GW2DBDisciplines discipline, int currentPage = 1)
        {
            int disciplineId;
            string disciplineStr;
            if (discipline == GW2DBDisciplines.Mystic_Forge) disciplineStr = "all";
            else
            {
                disciplineId = (int) discipline;
                disciplineStr = disciplineId.ToString();
            }
            String url = String.Format(@"http://www.gw2spidy.com/api/{0}/json/recipes/{1}/{2}", API_VERSION, disciplineStr, currentPage);

            return await Request(url);
        }

        //public async Task<Stream> RequestGw2dbItemTooltip(int itemId)
        //{
        //    String url = String.Format(@"http://www.gw2db.com/items/{0}/tooltip?x&advanced=1", itemId);

        //    return await Request(url);
        //}

        private async Task<Stream> Request(String url, bool acceptGzip = true, bool acceptDeflate = true)
        {
            Stream stream = null;
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.UseCookies = false;
                handler.UseDefaultCredentials = false;
                HttpClient client = new HttpClient(handler);

                client.MaxResponseContentBufferSize = 3000000;

                if (acceptGzip) client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                if (acceptDeflate) client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
                client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("en"));
                client.DefaultRequestHeaders.Connection.Add(@"keep-alive");
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(@"Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.19 (KHTML, like Gecko) Chrome/18.0.1003.1 Safari/535.19 Awesomium/1.7.1");
                client.DefaultRequestHeaders.Accept.TryParseAdd(@"*/*");
                client.DefaultRequestHeaders.Add(@"X-Requested-With", @"XMLHttpRequest");

                await client.GetAsync(url).ContinueWith(
                      (getTask) =>
                      {
                          if (getTask.IsCanceled)
                          {
                              return;
                          }
                          if (getTask.IsFaulted)
                          {
                              throw getTask.Exception;
                          }
                          HttpResponseMessage getResponse = getTask.Result;
                          getResponse.EnsureSuccessStatusCode();
                          stream = getResponse.Content.ReadAsStreamAsync().Result;
                          stream = ConnectionManager.ProcessCompression(stream, getResponse);
                       });
            }
            catch (Exception e)
            {
                throw e;
            }

            return stream;
        }
    }
}
