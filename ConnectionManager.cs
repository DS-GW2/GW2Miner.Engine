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
using System.IO.Compression;
using GW2SessionKey;

namespace GW2Miner.Engine
{
    /// <summary>
    /// TODO: Add MumbleLink class to access Mumble shared memory?
    /// </summary>
    public class ConnectionManager
    {
        /// <summary>
        /// TODO: Add these to config 
        /// </summary>
        private static readonly int RETRY_COOLDOWN = 4000;
        private static readonly int MAX_FLOOD_CONTROL_TIMESLOTS = 3;
        private static readonly double FLOOD_CONTROL_MS = RETRY_COOLDOWN * MAX_FLOOD_CONTROL_TIMESLOTS;
        private static readonly int RETRY_LIMIT = 3;

        private static TimeSlots _timeSlots = new TimeSlots(MAX_FLOOD_CONTROL_TIMESLOTS, FLOOD_CONTROL_MS);
        private static ConnectionManager _singleton;
        private static Object _classLock = typeof(ConnectionManager);

        public static Configuration _config;
        private String _loginURL = @"https://account.guildwars2.com/login?redirect_uri=http%3A%2F%2Ftradingpost-live.ncplatform.net%2Fauthenticate%3Fsource%3D%252F&game_code=gw2";
        //private String _loginURL = @"https://account.guildwars2.com/login?redirect_uri=http://tradingpost-live.ncplatform.net/authenticate?source=/me&game_code=gw2";
        //private String _loginURL = @"https://account.guildwars2.com/login?redirect_uri=http://tradingpost-live.ncplatform.net/authenticate?source=/me";
        private String _accountEmail = "dspirit@gmail.com"; // - empty for now
        private String _accountPassword = "thhedh11"; // - empty for now
        private bool _logined = false;
        private bool _loggingIn = false;
        private bool _useGameSessionKey = false;
        private bool _catchExceptions = true;
        private event EventHandler _fnCallGW2LoginInstructions;
        private Object _fnCallGW2LoginInstructionsLock = new Object();
        private event EventHandler _fnGW2Logined;
        private Object _fnGW2LoginedLock = new Object();
        //private ManualResetEvent _connected = new ManualResetEvent(false);
        //public ManualResetEvent _requested = new ManualResetEvent(false);
        private Stream _stream = null;
        CookieContainer _cookieJar = new CookieContainer();
        private Cookie _gameSessionKey;
        //private Cookie _mySessionKey;
        private int _retryRequest = 0;
        private string _charId, _loginEmail;

        public static ConnectionManager Instance
        {
            get
            {
                lock (_classLock)
                {
                    return _singleton ?? (_singleton = new ConnectionManager());
                }
            }
        }

        public static Stream ProcessCompression(Stream stream, HttpResponseMessage response)
        {
            Stream processedStream = stream;

            if (response.Content.Headers.ContentEncoding.Contains(gzip.Value))
            {
                processedStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            }
            else if (response.Content.Headers.ContentEncoding.Contains(deflate.Value))
            {
                processedStream = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress);
            }

            return processedStream;
        }

        private ConnectionManager()
        {
            string configFilePath = string.Format("{0}\\GW2TP.Config", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            // Map the new configuration file.
            ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap();
            configFileMap.ExeConfigFilename = configFilePath;

            // Open App.Config of executable
            _config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None); // potential exception if config file cannot be loaded!
            if (_config.AppSettings.Settings["SessionKey"] != null) _gameSessionKey = new Cookie("s", _config.AppSettings.Settings["SessionKey"].Value);
            if (_config.AppSettings.Settings["CharId"] != null) _charId = _config.AppSettings.Settings["CharId"].Value;

            // Must be done after getting the _gameSessionKey from the config
            //if (_useGameSessionKey) UseGameSessionKey = true;
            UseGameSessionKey = _useGameSessionKey;
        }

        public async Task<Stream> RequestItems(String page, String param, bool relogin)
        {
            String url;
            Uri referrer;
            lock (_classLock)
            {
                url = String.Format(@"https://tradingpost-live.ncplatform.net/ws/{0}.json?{1}", page, param);
                referrer = new Uri(@"https://tradingpost-live.ncplatform.net");
            }

            return await Request(url, referrer, relogin);
        }

        public async Task<Stream> RequestBuySellListing(int item_id, bool isBuyRequest, bool relogin)
        {
            String url;
            Uri referrer;

            lock (_classLock)
            {
                url = String.Format(@"https://tradingpost-live.ncplatform.net/ws/listings.json?type={0}&id={1}", (isBuyRequest ? "buys" : "sells"),
                                                    item_id);
                referrer = new Uri(String.Format(@"https://tradingpost-live.ncplatform.net/item/{0}", item_id));

                //UseGameSessionKey = true;
            }

            return await Request(url, referrer, relogin);
        }

        public async Task<Stream> CancelBuySellListing(int item_id, long listing_id, bool isBuyRequest, bool relogin)
        {
            String url;
            Uri referrer;

            lock (_classLock)
            {
                url = String.Format(@"https://tradingpost-live.ncplatform.net/ws/item/{0}/cancel.json?listing={1}&isbuy={2}&charid={3}", item_id,
                                                                                                                                            listing_id,
                                                                                                                                            (isBuyRequest ? "1" : "0"),
                                                                                                                                            _charId);
                referrer = new Uri(String.Format(@"https://tradingpost-live.ncplatform.net/me"));

                UseGameSessionKey = true;
            }

            return await Post(url, referrer, relogin, new List<KeyValuePair<string, string>>());
        }

        public async Task<Stream> Buy(int item_id, int count, int price, bool relogin)
        {
            String url;
            Uri referrer;
            List<KeyValuePair<string, string>> postData;

            lock (_classLock)
            {
                url = String.Format(@"https://tradingpost-live.ncplatform.net/ws/item/{0}/buy", item_id);

                referrer = new Uri(String.Format(@"https://tradingpost-live.ncplatform.net/"));

                UseGameSessionKey = true;

                postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("count", count.ToString()));
                postData.Add(new KeyValuePair<string, string>("price", price.ToString()));
                postData.Add(new KeyValuePair<string, string>("charid", _charId));
            }

            return await Post(url, referrer, relogin, postData);
        }

        public async Task<Stream> RequestMyBuysSells(bool buy, bool relogin, int offset = 1, bool past = false, int count = 10)
        {
            //await Request(@"https://tradingpost-live.ncplatform.net/me", new Uri(@"https://tradingpost-live.ncplatform.net"), false);
            String url;
            Uri referrer;
            lock (_classLock)
            {
                url = String.Format(@"https://tradingpost-live.ncplatform.net/ws/me.json?time={0}&type={1}&charid={2}&offset={3}&count={4}",
                                                                                                                    (past ? "past" : "now"),
                                                                                                                    (buy ? "buy" : "sell"),
                                                                                                                    _charId,
                                                                                                                    offset,
                                                                                                                    count);
                referrer = new Uri(@"https://tradingpost-live.ncplatform.net/me");

                UseGameSessionKey = true;
            }

            return await Request(url, referrer, relogin);
        }

        public async Task<Stream> RequestGemPrice(int coinQuantity, int gemQuantity)
        {
            String url;
            Uri referrer;
            lock (_classLock)
            {
                url = String.Format(@"https://exchange-live.ncplatform.net/ws/rates.json?id={0}&coins={1}&gems={2}", _charId, coinQuantity, gemQuantity);
                referrer = new Uri(@"https://exchange-live.ncplatform.net/");

                UseGameSessionKey = true;
            }

            return await Request(url, referrer, false);
        }

        public bool UseGameSessionKey
        {
            get
            {
                return _useGameSessionKey;
            }

            set
            {
                _cookieJar = new CookieContainer();
                _logined = false;
                _useGameSessionKey = value;
                if (_useGameSessionKey)
                {
                    _cookieJar.Add(new Uri("https://tradingpost-live.ncplatform.net/"), _gameSessionKey);
                    _cookieJar.Add(new Uri("https://exchange-live.ncplatform.net/"), _gameSessionKey);
                }
            }
        }

        public bool CatchExceptions
        {
            get
            {
                return _catchExceptions;
            }

            set
            {
                _catchExceptions = value;
            }
        }

        public event EventHandler FnLoginInstructions
        {
            add
            {
                lock (_fnCallGW2LoginInstructionsLock)
                {
                    _fnCallGW2LoginInstructions += value;
                }
            }

            remove
            {
                lock (_fnCallGW2LoginInstructionsLock)
                {
                    _fnCallGW2LoginInstructions -= value;
                }
            }
        }

        public event EventHandler FnGW2Logined
        {
            add
            {
                lock (_fnGW2LoginedLock)
                {
                    _fnGW2Logined += value;
                }
            }

            remove
            {
                lock (_fnGW2LoginedLock)
                {
                    _fnGW2Logined -= value;
                }
            }
        }

        private static readonly StringWithQualityHeaderValue gzip = StringWithQualityHeaderValue.Parse("gzip");
        private static readonly StringWithQualityHeaderValue deflate = StringWithQualityHeaderValue.Parse("deflate");

        //private void SwapSessionKey(Uri uri, Cookie newSessionKey)
        //{
        //    if (_cookieJar.Count > 0)
        //    {
        //        CookieCollection cookies = _cookieJar.GetCookies(uri);
        //        _mySessionKey = cookies["s"];
        //        if (_mySessionKey != null)
        //        {
        //            cookies["s"].Expires = DateTime.Now.AddDays(-1d);
        //            cookies["s"].Expired = true;
        //        }
        //    }
        //    if (newSessionKey != null)
        //    {
        //        _cookieJar.Add(uri, newSessionKey);
        //    }
        //}

        private async Task<Stream> Post(String url, Uri referrer, bool relogin, List<KeyValuePair<string, string>> postData, bool acceptGzip = true, bool acceptDeflate = true)
        {
            if (relogin)
            {
                _logined = false;
            }

            if (!_logined)
            {
                await Login();
            }

            WaitForFloodControl();

            try
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    CookieContainer = _cookieJar
                };
                handler.UseCookies = true;
                handler.UseDefaultCredentials = false;

                HttpContent content = new FormUrlEncodedContent(postData);

                HttpClient client = new HttpClient(handler);

                client.MaxResponseContentBufferSize = 3000000;

                client.DefaultRequestHeaders.Referrer = referrer;

                if (acceptGzip) client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                if (acceptDeflate) client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
                client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("en"));
                client.DefaultRequestHeaders.Connection.Add(@"keep-alive");
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(@"Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.19 (KHTML, like Gecko) Chrome/18.0.1003.1 Safari/535.19 Awesomium/1.7.1");
                client.DefaultRequestHeaders.Accept.TryParseAdd(@"*/*");
                client.DefaultRequestHeaders.Add(@"X-Requested-With", @"XMLHttpRequest");

                await client.PostAsync(url, content).ContinueWith(
                      (postTask) =>
                      {
                          if (postTask.IsCanceled)
                          {
                              return;
                          }
                          if (postTask.IsFaulted)
                          {
                              throw postTask.Exception;
                          }
                          HttpResponseMessage postResponse = postTask.Result;

                          // NOTE: 401 Status Code here if session key has expired!
                          if (postResponse.StatusCode == HttpStatusCode.Unauthorized && (_retryRequest == 0))
                          {
                              if (UseGameSessionKey)
                              {
                                  GetGameClientInfo();
                              }
                              _retryRequest++;
                              Task t = Task.Run(async () => { return await Post(url, referrer, true, postData, acceptGzip, acceptDeflate); });
                              t.Wait();
                              return;
                          }
                          else if ((postResponse.StatusCode == HttpStatusCode.ServiceUnavailable || postResponse.StatusCode == HttpStatusCode.BadGateway || 
                                                        postResponse.StatusCode == HttpStatusCode.InternalServerError)
                                        && (_retryRequest < RETRY_LIMIT))
                          {
                              Thread.Sleep(RETRY_COOLDOWN);
                              _retryRequest++;
                              Task t = Task.Run(async () => { return await Post(url, referrer, false, postData, acceptGzip, acceptDeflate); });
                              t.Wait();
                          }
                          else
                          {
                              _retryRequest = 0;
                              postResponse.EnsureSuccessStatusCode();
                              _stream = postResponse.Content.ReadAsStreamAsync().Result;
                              _stream = ProcessCompression(_stream, postResponse);
                              //if (postResponse.Content.Headers.ContentEncoding.Contains(gzip.Value))
                              //{
                              //    _stream = new System.IO.Compression.GZipStream(_stream, System.IO.Compression.CompressionMode.Decompress);
                              //}
                              //else if (postResponse.Content.Headers.ContentEncoding.Contains(deflate.Value))
                              //{
                              //    _stream = new System.IO.Compression.DeflateStream(_stream, System.IO.Compression.CompressionMode.Decompress);
                              //}
                          }
                      });
            }
            catch (Exception e)
            {
                if (!_catchExceptions) throw e;

                // handle error
                Console.WriteLine("Post Exception:");
                Console.WriteLine(e.Message);
            }

            return _stream;
        }

        private async Task<Stream> Request(String url, Uri referrer, bool relogin, bool acceptGzip = true, bool acceptDeflate = true)
        {
            if (relogin)
            {
                _logined = false;
            }

            if (!_logined)
            {
                await Login();
            }

            WaitForFloodControl();

            try
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    CookieContainer = _cookieJar
                };
                handler.UseCookies = true;
                handler.UseDefaultCredentials = false;
                HttpClient client = new HttpClient(handler);

                client.MaxResponseContentBufferSize = 3000000;

                client.DefaultRequestHeaders.Referrer = referrer;

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

                            // NOTE: 401 Status Code here if session key has expired!
                            if (getResponse.StatusCode == HttpStatusCode.Unauthorized && (_retryRequest == 0))
                            {
                                if (UseGameSessionKey)
                                {
                                    GetGameClientInfo();
                                }
                                _retryRequest++;
                                Task t = Task.Run(async () => { return await Request(url, referrer, true, acceptGzip, acceptDeflate); });
                                t.Wait();
                                return;
                            }
                            else if ((getResponse.StatusCode == HttpStatusCode.ServiceUnavailable || getResponse.StatusCode == HttpStatusCode.BadGateway ||
                                                        getResponse.StatusCode == HttpStatusCode.InternalServerError)
                                        && (_retryRequest < RETRY_LIMIT))
                            {
                                Thread.Sleep(RETRY_COOLDOWN);
                                _retryRequest++;
                                Task t = Task.Run(async () => { return await Request(url, referrer, false, acceptGzip, acceptDeflate); });
                                t.Wait();
                            }
                            else
                            {
                                _retryRequest = 0;
                                getResponse.EnsureSuccessStatusCode();
                                _stream = getResponse.Content.ReadAsStreamAsync().Result;
                                _stream = ProcessCompression(_stream, getResponse);
                                //if (getResponse.Content.Headers.ContentEncoding.Contains(gzip.Value))
                                //{
                                //    _stream = new System.IO.Compression.GZipStream(_stream, System.IO.Compression.CompressionMode.Decompress);
                                //}
                                //else if (getResponse.Content.Headers.ContentEncoding.Contains(deflate.Value))
                                //{
                                //    _stream = new System.IO.Compression.DeflateStream(_stream, System.IO.Compression.CompressionMode.Decompress);
                                //}
                            }
                        });
            }
            catch (Exception e)
            {
                if (!_catchExceptions) throw e;

                // handle error
                Console.WriteLine("Request Exception:");
                Console.WriteLine(e.Message);
            }

            return _stream;
        }

        private async Task Login()
        {
            if (UseGameSessionKey)
            {
                IndicateLogined();
                return;
            }

            if (!_loggingIn)
            {
                _loggingIn = true;

                WaitForFloodControl();

                try
                {
                    HttpClientHandler handler = new HttpClientHandler()
                    {
                        CookieContainer = _cookieJar
                    };                    
                    handler.UseCookies = true;
                    handler.UseDefaultCredentials = false;
                    handler.AllowAutoRedirect = false;
                    HttpClient client = new HttpClient(handler);

                    List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("email", _accountEmail));
                    postData.Add(new KeyValuePair<string, string>("password", _accountPassword));

                    HttpContent content = new FormUrlEncodedContent(postData);

                    client.DefaultRequestHeaders.Referrer = new Uri(@"https://account.guildwars2.com/login");

                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
                    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
                    client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("en"));
                    client.DefaultRequestHeaders.Connection.Add(@"keep-alive");
                    client.DefaultRequestHeaders.UserAgent.TryParseAdd(@"Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.19 (KHTML, like Gecko) Chrome/18.0.1003.1 Safari/535.19 Awesomium/1.7.1");
                    client.DefaultRequestHeaders.Accept.TryParseAdd(@"text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                    await client.PostAsync(_loginURL, content).ContinueWith(
                        (postTask) =>
                        {
                            if (postTask.IsCanceled)
                            {
                                return;
                            }
                            if (postTask.IsFaulted)
                            {
                                throw postTask.Exception;
                            }

                            HttpResponseMessage postResponse = postTask.Result;

                            // NOTE: 401 Status Code here if traditional login failed!
                            if (postResponse.StatusCode == HttpStatusCode.Unauthorized && !UseGameSessionKey)
                            {
                                if (_catchExceptions)
                                {
                                    Console.WriteLine("URL Login failed!  Falling back to using game session key!");
                                }
                                UseGameSessionKey = true;
                            }
                            else
                            {
                                //postTask.Result.EnsureSuccessStatusCode();
                                int i;
                                IEnumerable<string> cookies = postResponse.Headers.GetValues("Set-Cookie");
                                if (cookies != null && (i = cookies.ElementAt(0).IndexOf("s=")) >= 0)
                                {
                                    string value = cookies.ElementAt(0).Substring(i + 2, cookies.ElementAt(0).IndexOf(';', i) - i - 2);
                                    Cookie sessionKey = new Cookie("s", value);
                                    _cookieJar.Add(new Uri("https://tradingpost-live.ncplatform.net/"), sessionKey);
                                    _cookieJar.Add(new Uri("https://exchange-live.ncplatform.net/"), sessionKey);
                                }
                                else
                                {
                                    UseGameSessionKey = true;
                                }
                            }
                            IndicateLogined();
                        });
                }
                catch (Exception e)
                {
                    if (UseGameSessionKey)
                    {
                        if (!_catchExceptions) throw e;

                        // handle error
                        Console.WriteLine("Login Exception:");
                        Console.WriteLine(e.Message);
                    }
                    else
                    {
                        UseGameSessionKey = true;
                        IndicateLogined();
                    }
                }
            }
        }

        private void IndicateLogined()
        {
            _logined = true;
            _loggingIn = false;
        }

        private void WaitForFloodControl()
        {
            _timeSlots.GetSlot();
        }

        private void GetGameClientInfo()
        {
            lock (_classLock)
            {
                if (_fnCallGW2LoginInstructions != null) _fnCallGW2LoginInstructions(this, EventArgs.Empty);
                else
                {
                    Console.WriteLine("Getting new session key from Guild Wars 2 client.  Please ensure that Guild Wars 2 is running, logined to the game, and open the trading post.");
                    Console.WriteLine("Hit ENTER to continue...");
                    Console.ReadLine();
                }

                Process[] processes = Process.GetProcessesByName("Gw2");

                while (processes.Length == 0)
                {
                    Thread.Sleep(10000);
                    processes = Process.GetProcessesByName("Gw2");
                }

                //if (processes.Length == 0)
                //{
                //    throw new InvalidOperationException("Guild Wars 2 client NOT detected!  Please ensure that Guild Wars 2 is running and logined to the game.");
                //}
                Process p = processes[0];

                ProcessMemoryScanner scanner = new ProcessMemoryScanner(p);

                //TODO: Grab search pattern from config file instead
                // find current session key
                string szSearchPattern = "8B4214A3xxxxxxxx";

                Guid sessionKey = scanner.FindGuid(szSearchPattern, 4);

                while (sessionKey == Guid.Empty)
                {
                    sessionKey = scanner.FindGuid(szSearchPattern, 4);
                    Thread.Sleep(1000);
                }

                //if (sessionKey == Guid.Empty)
                //{
                //    throw new InvalidOperationException("Please ensure that Guild Wars 2 is running and logined to the game.");
                //}

                string sessionKeyString = string.Format("{0}", sessionKey.ToString().ToUpper());

                _gameSessionKey = new Cookie("s", sessionKeyString);

                if (_config.AppSettings.Settings["SessionKey"] != null)
                    _config.AppSettings.Settings["SessionKey"].Value = sessionKeyString;
                else
                    _config.AppSettings.Settings.Add("SessionKey", sessionKeyString);

                //processes = Process.GetProcessesByName("awesomium_process");

                //while (processes.Length == 0)
                //{
                //    Thread.Sleep(10000);
                //    processes = Process.GetProcessesByName("awesomium_process");
                //}

                //TODO: Grab search pattern from config file instead
                //      or grab the character id guid from Mumble shared memory instead
                // find current character id
                szSearchPattern = "898560FFFFFFA1xxxxxxxx";

                Guid charId = scanner.FindGuid(szSearchPattern, 7);
                _charId = string.Format("{0}", charId.ToString().ToUpper());

                if (_config.AppSettings.Settings["CharId"] != null)
                    _config.AppSettings.Settings["CharId"].Value = _charId;
                else
                    _config.AppSettings.Settings.Add("CharId", _charId);


                //TODO: Grab search pattern from config file instead
                //CPU Disasm
                //Address   Hex dump          Command                                  Comments
                //006E133F      CC            INT3
                //006E1340  /$  8BD1          MOV EDX,ECX
                //006E1342  |.  68 80000000   PUSH 80                                  ; /Arg1 = 80
                //006E1347  |.  B9 98A06601   MOV ECX,OFFSET 0166A098                  ; |ASCII "clientreport@arena.net"
                //006E134C  |.  E8 4F6DFEFF   CALL 006C80A0                            ; \Gw2.006C80A0
                //006E1351  \.  C3            RETN
                //006E1352      CC            INT3
                szSearchPattern = "C3CCCCCCCCCCCCCC8BD16880000000B9xxxxxxxxE8xxxxxxxxC3CC";

                String _loginEmail = scanner.FindString(szSearchPattern, 16, 255);

                _config.Save(ConfigurationSaveMode.Modified);

                ConfigurationManager.RefreshSection("appSettings");

                _cookieJar = new CookieContainer();
                _cookieJar.Add(new Uri("https://tradingpost-live.ncplatform.net/"), _gameSessionKey);
                _cookieJar.Add(new Uri("https://exchange-live.ncplatform.net/"), _gameSessionKey);

                WaitForGameSession();

                if (_fnGW2Logined != null) _fnGW2Logined(this, EventArgs.Empty);

                return;
            }
        }

        private void WaitForGameSession()
        {
            bool tempExceptionsSetting = _catchExceptions;
            _catchExceptions = false;
            UseGameSessionKey = true;
            _retryRequest = RETRY_LIMIT;

            bool needToWait = true;
            while (needToWait)
            {
                try
                {
                    needToWait = false;
                    Task t = RequestGemPrice(10000, 100);
                    t.Wait();
                }
                catch (Exception)
                {
                    needToWait = true;
                }

                Thread.Sleep(10000);
            }

            _catchExceptions = tempExceptionsSetting;
            _retryRequest = 0;
        }
    }
}
