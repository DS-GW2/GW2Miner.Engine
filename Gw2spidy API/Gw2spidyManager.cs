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
    /// <summary>
    /// TODO: The strength of gw2spidy is the constant monitoring of prices.  Add routine to calculate the monthly average buy and sell prices for the last month, for an item.
    /// </summary>
    public class Gw2spidyManager
    {
        public async Task<Stream> RequestGw2spidyItem(int itemId)
        {
            String url = String.Format(@"http://www.gw2spidy.com/api/v0.9/json/item/{0}", itemId);

            return await Request(url);
        }

        //public async Task<Stream> RequestGw2dbItemTooltip(int itemId)
        //{
        //    String url = String.Format(@"http://www.gw2db.com/items/{0}/tooltip?x&advanced=1", itemId);

        //    return await Request(url);
        //}

        private async Task<Stream> Request(String url)
        {
            Stream stream = null;
            try
            {
                HttpClientHandler handler = new HttpClientHandler();
                handler.UseCookies = false;
                handler.UseDefaultCredentials = false;
                HttpClient client = new HttpClient(handler);

                client.MaxResponseContentBufferSize = 3000000;

                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
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
