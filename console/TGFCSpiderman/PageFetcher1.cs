using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.TaskQueue;

namespace taiyuanhitech.TGFCSpiderman
{
    class PageFetcher1 : TaskRunner<string>
    {
        private readonly string _userName;
        private readonly string _password;
        private readonly HttpClient _httpClient;


        public PageFetcher1(string userName, string password)
        {
            _userName = userName;
            _password = password;

            var handler = new HttpClientHandler()
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                UseDefaultCredentials = false,
            };
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://wap.tgfcer.com/"),
                Timeout = TimeSpan.FromSeconds(120)
            };
            _httpClient.DefaultRequestHeaders.Add("user-agent", "Opera/9.80 (Windows NT 5.1) Presto/2.12.388 Version/12.16");
        }

        protected override void BeforeRun()
        {
            Signin();
        }

        protected override void ExecuteTask(List<string> tasks)
        {
            //Interlocked.Increment(ref _httpRequestCounter);
            //FetchPageContent(task).ContinueWith(t =>
            //{
            //    //Console.WriteLine("Fetched page:" + t.Result.Url);
            //    TaskQueueManager.Inst.PageMill.EnqueueTask(new MillRequest{Url = task, HtmlContent = t.Result.Content});
            //    Interlocked.Decrement(ref _httpRequestCounter);
            //}).Wait();
            //if you want to run tasks parallel ,get rid of the Wait() call.
        }

        private void Signin()
        {
            const string url = "index.php?action=login&sid=&vt=1&tp=100&pp=100&sc=0&vf=0&sm=0&iam=&css=&verify=";
            HttpContent content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", _userName),
                new KeyValuePair<string, string>("password", _password),
                new KeyValuePair<string, string>("login", "登录"),
                new KeyValuePair<string, string>("vt", "1")
            });

            _httpClient.PostAsync(url, content).Wait();
            //responseMessage.EnsureSuccessStatusCode(); 没有用，密码错误时StatusCode也是200，只能根据response content 判断是否登录成功。
            //TODO:判断登录是否成功，若不成功，或许可以尝试换一个用户名和密码，现阶段登录正常，不需要做这么细。
            //TODO:考虑cookie过期的问题，在请求论坛内容的过程中，可能因为cookie过期而无法完成请求，需重新登录。现阶段尚未发现cookie过期的情况。
        }

        private async Task<PageFetchResult> FetchPageContent(string url)
        {
            try
            {
                string content = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                return new PageFetchResult
                {
                    Url = url,
                    Content = content
                };
            }
            catch (HttpRequestException)
            {
                return new PageFetchResult
                {
                    Url = url,
                    Content = null
                };
            }
            catch (Exception)
            {
                return new PageFetchResult
                {
                    Url = url,
                    Content = null
                };
            }
        }

        private async Task<byte[]> FetchFile(string url)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            return null;
        }
    }
}
