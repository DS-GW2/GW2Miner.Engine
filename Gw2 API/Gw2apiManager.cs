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

        public async Task<Stream> RequestGw2apiItem(int itemId)
        {
            String url = String.Format(@"https://api.guildwars2.com/v1/item_details.json?item_id={0}", itemId);

            return await Request(url);
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

        public async Task<Stream> RequestGw2apiRecipe(int recipeId)
        {
            String url = String.Format(@"https://api.guildwars2.com/v1/recipe_details.json?recipe_id={0}", recipeId);

            return await Request(url);
        }

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
