using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Threading;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.JobQueue;

namespace taiyuanhitech.TGFCSpiderman
{
    public class TaskQueueManager
    {
        private const int InitialRetryInterval = 100;//TODO:configurable
        private const int MaxRetryInterval = InitialRetryInterval * (2 << 13);
        private static readonly TaskQueueManager _inst;
        private readonly IPageFetcher _pageFetcher;
        private readonly IPageProcessor _pageProcessor;
        private readonly PageFetchJobRunner _pageFetchJobRunner;
        private readonly PageMillJobRunner _pageMillJobRunner;
        private readonly PostSaveJobRunner _pageSaveJobRunner;
        private string _userName;
        private string _password;

        static TaskQueueManager()
        {
            _inst = new TaskQueueManager();
        }

        private TaskQueueManager()
        {
            _pageFetchJobRunner = new PageFetchJobRunner(_pageFetcher = ComponentFactory.GetPageFetcher());
            _pageFetchJobRunner.Run();
            _pageMillJobRunner = new PageMillJobRunner(_pageProcessor = ComponentFactory.GetPageProcessor());
            _pageMillJobRunner.Run();
            _pageSaveJobRunner = new PostSaveJobRunner(ComponentFactory.GetPostRepository());
            _pageSaveJobRunner.Run();
        }

        public static TaskQueueManager Inst
        {
            get { return _inst; }
        }

        public void Run(string userName, string password, DateTime expirationDate)
        {
            _userName = userName;
            _password = password;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                var entryPointUrl = "index.php?action=forum&fid=25&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default";
                for (; ; )
                {
                    if (entryPointUrl == null)
                        return;
                    Console.WriteLine("正在获取论坛第 {0} 页。", entryPointUrl.GetPageIndex());
                    var entryPointFetchResult = FetchPage(entryPointUrl);
                    Console.WriteLine("获取完成，开始处理。");

                    MillResult<List<ThreadHeader>> forumPageMillResult;
                    MillStatus processStatus = _pageProcessor.TryProcessPage(
                            new MillRequest { Url = entryPointUrl, HtmlContent = entryPointFetchResult },
                            out forumPageMillResult);
                    if (processStatus == MillStatus.FormatError)
                    {
                        Console.WriteLine("分析网页结构时发生错误，本系统将停止执行。Url : {0}", entryPointUrl);
                        return;
                    }
                    if (processStatus == MillStatus.NotSignedIn)
                    {
                        _pageFetcher.Signin(userName, password);//TODO:what if it throws?
                        continue;
                    }
                    if (processStatus == MillStatus.PermissionDenied)
                    {
                        Console.WriteLine("您提供的用户没有访问主题列表的权限，本系统将停止执行。Url : {0}", entryPointUrl);
                        return;
                    }

                    //找到最后一个thread 的最后reply date
                    List<ThreadHeader> threadHeasers = forumPageMillResult.Result;
                    int currentIndex = threadHeasers.Count - 1;
                    for (; ; )
                    {
                        if (currentIndex < 0)
                        {
                            //一页的所有thread都没有权限看
                            entryPointUrl = forumPageMillResult.NextPageUrl;
                            break;
                        }
                        var header = threadHeasers[currentIndex];
                        var lastReplyPageUrl = header.Url.ChangePageIndex(header.GetLastPageIndex());
                        Console.WriteLine("正在获取主题列表第 {0} 个主题最后一页。", currentIndex + 1);
                        string threadLastPageFetchResult = FetchPage(lastReplyPageUrl);
                        Console.WriteLine("获取完成。");
                        MillResult<ForumThread> threadLastPageProcessResult;
                        processStatus = _pageProcessor.TryProcessPage(
                                new MillRequest { Url = lastReplyPageUrl, HtmlContent = threadLastPageFetchResult },
                                out threadLastPageProcessResult);

                        if (processStatus == MillStatus.NotSignedIn)
                        {
                            _pageFetcher.Signin(userName, password);
                            //TODO:what if it throws UserNameOrPasswordException? Need to impllment ask password and re-enter.
                            continue;
                        }
                        if (processStatus == MillStatus.FormatError)
                        {
                            Console.WriteLine("分析网页结构时发生错误，本系统将停止执行。Url : {0}", lastReplyPageUrl);
                            return;
                        }
                        if (processStatus == MillStatus.PermissionDenied)
                        {
                            Console.WriteLine("您提供的用户没有访问该主题的权限，正在尝试列表中的上一个主题。Url : {0}", entryPointUrl);
                            currentIndex--; //尝试上一个thread
                            continue;
                        }

                        var posts = threadLastPageProcessResult.Result.Posts;
                        var lastReplyDate = posts.Last().ModifyDate ?? posts.Last().CreateDate;

                        if (lastReplyDate < expirationDate)
                        {
                            //todo:找出确切的最晚的一个早于expirationDate的thread，处理这个thread及比它更晚（在threadHeaders中序号更小）的thread
                            HandleThreads(threadHeasers);
                            return; //全处理完了，没了
                        }
                        else
                        {
                            //下一页可能还有更新的，先处理本页的，再到下一页看看。
                            HandleThreads(threadHeasers);
                            entryPointUrl = forumPageMillResult.NextPageUrl;
                            break;
                        }
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
                Console.WriteLine("运行完成，耗时 {0} 秒", stopwatch.Elapsed.TotalSeconds);
            }
        }

        private void HandleThreads(IEnumerable<ThreadHeader> threadHeasers)
        {
            _pageFetchJobRunner.EnqueueRange(
                threadHeasers.Select(t =>
                {
                    t.Url = t.Url.ChangePageIndex(t.GetLastPageIndex());
                    return new PageFetchRequest(t.Url, t.ToDescription());
                }));
            WaitHandle.WaitAll(new[] { _pageFetchJobRunner.IdleWaitHandle, _pageMillJobRunner.IdleWaitHandle, _pageSaveJobRunner.IdleWaitHandle });
        }

        public void OnPageFetchCompleted(PageFetchResult result)
        {
            if (result.Error == null)
            {
                if (result.Content != null)
                {
                    _pageMillJobRunner.EnqueueJob(new MillRequest
                    {
                        Url = result.Url,
                        HtmlContent = result.Content,
                        HumanReadableDescription = result.HumanReadableDescription
                    });
                }
                return;
            }

            if (result.Error.IsConnectionError)
            {//可能是连不上网，或者网站服务器关了
                Console.WriteLine(result.HumanReadableDescription.ChangeStatusInDescription("网络不通"));
                return;
            }
            if (result.Error.IsTimeout)
            {//超时
                Console.WriteLine(result.HumanReadableDescription.ChangeStatusInDescription("超时　　"));
                _pageFetchJobRunner.EnqueueJob(result);
                return;
            }
            if (result.Error.StatusCode != HttpStatusCode.OK)
            {//服务器返回错误信息
                Console.WriteLine(result.HumanReadableDescription.ChangeStatusInDescription("请求错误"));
                _pageFetchJobRunner.EnqueueJob(result);
                return;
            }
        }

        public void OnThreadPageMillCompleted(MillStatus status, MillResult<ForumThread> threadPage)
        {
            if (status == MillStatus.NotSignedIn)
            {
                _pageFetcher.Signin(_userName, _password);
                _pageFetchJobRunner.EnqueueJob(new PageFetchRequest(threadPage.Url, "postxxxxxxxxxxxxxxxx"));//TODO:fix it
                return;
            }
            if (status == MillStatus.PermissionDenied)
            {
                //看不了，无视
            }
            if (status == MillStatus.FormatError)
            {
                Console.WriteLine("thread page html 格式发生变化，无法查看。程序正在关闭...");
                _pageFetchJobRunner.Stop();
                _pageMillJobRunner.Stop();
                _pageSaveJobRunner.Stop();
            }
            if (status == MillStatus.Success)
            {
                Console.WriteLine(threadPage.HumanReadableDescription.ChangeStatusInDescription("处理完成"));
                _pageSaveJobRunner.EnqueueRange(threadPage.Result.Posts);
                if (threadPage.NextPageUrl != null)
                {
                    _pageFetchJobRunner.EnqueueJob(new PageFetchRequest(threadPage.NextPageUrl,
                        threadPage.HumanReadableDescription.ChangePageIndexInDescription(int.Parse(threadPage.NextPageUrl.GetPageIndex()))));
                }
            }
        }

        private string FetchPage(string url)
        {
            int retryInterval = InitialRetryInterval;
            PageFetchResult result;
            while ((result = _pageFetcher.Fetch(new PageFetchRequest(url, null)).Result).Content == null)
            {
                //TODO:log error 
                Thread.Sleep(TimeSpan.FromMilliseconds(retryInterval));
                retryInterval *= 2;
                if (retryInterval > MaxRetryInterval)
                    retryInterval = InitialRetryInterval;
            }

            return result.Content;
        }
    }

    public static class ThreadHeaderExt
    {
        public static string ChangePageIndex(this string url, int newPageIndex)
        {
            var r = new Regex("page=\\d+");
            if (r.IsMatch(url))
            {
                return r.Replace(url, "page=" + newPageIndex);
            }
            return url.EndsWith("&") ? url + "page=" + newPageIndex : url + "&page=" + newPageIndex;
        }

        public static int GetLastPageIndex(this ThreadHeader header)
        {
            const int postsPerPage = 100;
            return header.ReplyCount / postsPerPage + 1;
        }

        public static string ToDescription(this ThreadHeader header)
        {
            var pageIndex = header.Url.GetPageIndex();
            return string.Format("第{0, 4}页\t　　　　\t{1}", pageIndex, header.Title.ToFixedLength(20));
        }

        public static string ChangePageIndexInDescription(this string desc, int newPageIndex)
        {
            var re = new Regex(@"^第[ \d]{5}页");
            return re.Replace(desc, string.Format("第{0, 4}页", newPageIndex));
        }

        public static string ChangeStatusInDescription(this string desc, string status)
        {
            Regex statusRex = new Regex("\t.{4}\t");
            return statusRex.Replace(desc, "\t" + status.ToFixedLength(4) + "\t");
        }
    }

    public static class StringExt
    {
        public static string ToFixedLength(this string s, int length)
        {
            if (s == null)
                s = "";
            if (s.Length == length)
            {
                return s;
            }
            if (s.Length > length)
            {
                return s.Substring(0, length);
            }
            if (s.Length < length)
            {
                return string.Format("{0,-" + length + "}", s);
            }
            throw new Exception("weird!");
        }
    }
}