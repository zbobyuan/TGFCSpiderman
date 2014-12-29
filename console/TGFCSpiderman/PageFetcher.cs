using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpiderman
{
    internal class PageFetcher : IPageFetcher
    {
        private readonly IPageFetcherConfig _config;
        private readonly HttpClient _httpClient;
        private readonly ManualResetEvent _signinWaitHandle = new ManualResetEvent(true);

        public PageFetcher(IPageFetcherConfig config)
        {
            _config = config;
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                UseDefaultCredentials = false,
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://wap.tgfcer.com/"),
                Timeout = TimeSpan.FromSeconds(_config.TimeoutInSeconds) //_httpClient.GetAsync() throws TaskCanceledException
            };
            _httpClient.DefaultRequestHeaders.Add("user-agent", _config.UserAgent);
        }

        public async Task<PageFetchResult> Fetch(PageFetchRequest request)
        {
            _signinWaitHandle.WaitOne(); //Not a big deal, _signedWaitHandle is always signed.
            var result = new PageFetchResult(request);
            HttpResponseMessage responseMessage = null;
            try
            {
                responseMessage = await _httpClient.GetAsync(request.Url);
                responseMessage.EnsureSuccessStatusCode();
                using (var content = responseMessage.Content)
                {
                    result.Content = await content.ReadAsStringAsync();
                }
                return result;
            }
            catch (TaskCanceledException)
            {
                result.Error = new PageFetchResult.ErrorInfo
                {
                    IsTimeout = true
                };
                return result;
            }
            catch (HttpRequestException)
            {
                result.Error = new PageFetchResult.ErrorInfo();
                if (responseMessage == null)
                {
                    result.Error.IsConnectionError = true;
                    /*
                    if (hre.InnerException != null && hre.InnerException is WebException)
                    {
                        var status = ((WebException) (hre.InnerException)).Status;
                    }
                    // * */
                }
                else
                {
                    result.Error.StatusCode = responseMessage.StatusCode;
                }
                return result;
            }
        }

        public void Signin(string userName, string password)
        {
            _signinWaitHandle.Reset();
            bool signedIn = false;
            try
            {
                const string url = "index.php?action=login&sid=&vt=1&tp=100&pp=100&sc=0&vf=0&sm=0&iam=&css=&verify=";//TODO:configurable
                HttpContent content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", userName),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("login", "登录"),
                    new KeyValuePair<string, string>("vt", "1")
                });

                int retryTimes = 0;
                while (!signedIn && retryTimes < _config.SigninRetryTimes)
                {
                    try
                    {
                        var responseMessage = _httpClient.PostAsync(url, content).Result;
                        responseMessage.EnsureSuccessStatusCode();
                        using (var responseContent = responseMessage.Content)
                        {
                            var responseBody = HttpUtility.HtmlDecode(responseContent.ReadAsStringAsync().Result);
                            if (!GetSigninStatusFromResponse(userName, responseBody))
                            {
                                throw new UserNameOrPasswordException();
                            }
                            signedIn = true;
                        }
                    }
                    catch (UserNameOrPasswordException)
                    {
                        throw;
                    }
                    catch
                    {
                        retryTimes++;
                    }
                }
            }
            finally
            {
                _signinWaitHandle.Set();
            }
            if (!signedIn)
            {
                throw new CannotSigninException();
            }
        }

        private static bool GetSigninStatusFromResponse(string userName, string body)
        {
            return body.IndexOf(userName + "成功登录", StringComparison.CurrentCulture) > 0;
        }
    }
}