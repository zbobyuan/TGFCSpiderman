using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CsQuery.ExtensionMethods;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpiderman
{
    internal class PageFetcher : IPageFetcher, IDisposable
    {
        private static readonly Regex SigninMessageRegex = new Regex("<div>(.+)</div>");
        private readonly IPageFetcherConfig _config;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly ManualResetEvent _signinWaitHandle = new ManualResetEvent(true);
        private bool _hasAuthToken;

        public PageFetcher(IPageFetcherConfig config) : this(config, null)
        {
        }

        public PageFetcher(IPageFetcherConfig config, string authToken)
        {
            _config = config;
            _cookieContainer = new CookieContainer();
            if (!string.IsNullOrEmpty(authToken))
            {
                authToken.Split(',').ForEach(s =>
                {
                    var cookieInfo = s.Split('=');
                    _cookieContainer.Add(new Cookie(cookieInfo[0], cookieInfo[1], "/", ".tgfcer.com"));
                });
                _hasAuthToken = true;
            }
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
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
            _signinWaitHandle.WaitOne(); 
            var result = new PageFetchResult(request);
            HttpResponseMessage responseMessage = null;
            try
            {
                responseMessage = await _httpClient.GetAsync(request.Url).ConfigureAwait(false);
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

        public async Task<string> Signin(string userName, string password)
        {
            _signinWaitHandle.Reset();
            var signedIn = false;
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
                        ClearCookie();
                        var responseMessage = await _httpClient.PostAsync(url, content);
                        responseMessage.EnsureSuccessStatusCode();
                        using (var responseContent = responseMessage.Content)
                        {
                            var responseBody = HttpUtility.HtmlDecode(await responseContent.ReadAsStringAsync());
                            EnsureSignedIn(userName, responseBody);
                            signedIn = true;
                        }
                    }
                    catch (CannotSigninException)
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
            if (!signedIn) throw new CannotSigninException("登录不能，原因不明，自己到网页上登录试试吧。");
            _hasAuthToken = true;
            var cookies = _cookieContainer.GetCookies(new Uri("http://tgfcer.com/"));
            return string.Join(",",(from Cookie c in cookies select c.ToString()).ToArray());
        }

        public bool HasAuthToken
        {
            get { return _hasAuthToken; }
        }

        private void ClearCookie()
        {
            foreach (Cookie cookie in _cookieContainer.GetCookies(new Uri("http://tgfcer.com")))
            {
                cookie.Expired = true;
            }
        }

        private static void EnsureSignedIn(string userName, string responseBody)
        {
            var match = SigninMessageRegex.Match(responseBody);
            if (!match.Success) throw new CannotSigninException("登陆不能，不要问我没什么，自己看看吧。");
            var message = match.Groups[1].Value;
            if (message.IndexOf(userName + "成功登录", StringComparison.CurrentCulture) >= 0)
                return;
            throw new CannotSigninException(message);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _signinWaitHandle.Set();
            _signinWaitHandle.Dispose();
        }
    }
}