using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.JobQueue;
using taiyuanhitech.TGFCSpiderman.Persistence;

namespace taiyuanhitech.TGFCSpiderman
{
    public class TaskQueueManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const int InitialRetryInterval = 3000;//TODO:configurable
        private const int ForumPageMaxRetryTimes = 15;
        private const int ThreadPageMaxRetryTimes = 10;
        private const int PageFetchBatchSize = 3;
        private const int MaxRetryInterval = InitialRetryInterval * (2 << 10);
        private const int DelayInHttpRequests = InitialRetryInterval;
        private const int CycleInterval = 10;
        private readonly IPageFetcher _pageFetcher;
        private readonly IPageProcessor _pageProcessor;
        private readonly PostSaveJobRunner _pageSaveJobRunner;
        private readonly IRunningInfoRepository _runningInfoRepository;
        private Dictionary<string, int> _retryCounter;
        private readonly HashSet<int> _excludedThreadIds = new HashSet<int> { 7041501, 7018450, 6685754 };
        private readonly Action<string> _outputAction;

        public TaskQueueManager(IPageFetcher pageFetcher, IPageProcessor pageProcessor, Action<string> outputAction)
        {
            _pageFetcher = pageFetcher;
            _pageProcessor = pageProcessor;
            _outputAction = outputAction;
            _pageSaveJobRunner = new PostSaveJobRunner(ComponentFactory.GetPostRepository());
            _runningInfoRepository = ComponentFactory.GetRunningInfoRepository();
        }

        public async Task Run(RunningInfo runningInfo, CancellationToken ct)
        {
            if (runningInfo == null)
                throw new ArgumentNullException("runningInfo");

            var rawEntryPointUrl = runningInfo.InitialEntryPointUrl;
            var entryPointUrl = runningInfo.CurrentEntryPointUrl ?? rawEntryPointUrl;
            var mode = runningInfo.Mode;
            var expirationDate = runningInfo.CurrentExpirationDate;
            var lastCycleStartTime = DateTime.Now;
            var fetchQueue = new Queue<PageFetchRequest>();
            _retryCounter = new Dictionary<string, int>();
            _pageSaveJobRunner.Run();
            var watch = Stopwatch.StartNew();

            try
            {
                var oldestReplyDateTillNow = DateTime.MaxValue;
                for (; ; )
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entryPointUrl))
                    {
                        if (mode == RunningInfo.RunningMode.Single)
                        {
                            runningInfo.IsCompleted = true;
                            return;
                        }
                        else
                        {
                            entryPointUrl = rawEntryPointUrl;
                            expirationDate = lastCycleStartTime;
                            _outputAction(string.Format("运行完成，等待{0}分钟后重新开始。", CycleInterval));
                            await Task.Delay(CycleInterval * 60 * 1000, ct);

                            lastCycleStartTime = DateTime.Now;
                        }
                    }

                    runningInfo.CurrentEntryPointUrl = entryPointUrl;
                    runningInfo.CurrentExpirationDate = expirationDate;
                    try
                    {
                        await _runningInfoRepository.SaveAsync(runningInfo);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }//即使保存失败也没关系
                    _outputAction(string.Format("正在获取论坛第 {0} 页，截止时间:{1}。", entryPointUrl.GetPageIndex(), expirationDate));

                    var entryPointHtmlContent = await FetchPageContent(entryPointUrl, ForumPageMaxRetryTimes, ct);
                    if (entryPointHtmlContent == null)
                    {
                        var error = string.Format("无法获取论坛第 {0} 页，URL:{1}\r\n退出运行。", entryPointUrl.GetPageIndex(), entryPointUrl);
                        Logger.Error(error);
                        _outputAction(error);
                        throw new Exception("erorr");
                    }

                    MillResult<List<ThreadHeader>> forumPageMillResult;
                    var processStatus = _pageProcessor.TryProcessPage(new MillRequest { Url = entryPointUrl, HtmlContent = entryPointHtmlContent },
                            out forumPageMillResult);

                    if (processStatus != MillStatus.Success)
                    {
                        HandleForumPageMillError(entryPointUrl, processStatus);
                        return;
                    }
                    ct.ThrowIfCancellationRequested();
                    var threadHeaders = forumPageMillResult.Result.Where(t => !_excludedThreadIds.Contains(t.Id)).ToList();
                    var currentIndex = 0;
                    while (currentIndex < threadHeaders.Count)
                    {
                        ct.ThrowIfCancellationRequested();
                        var requests = new List<PageFetchRequest>(PageFetchBatchSize);
                        for (var i = 0; i < PageFetchBatchSize && currentIndex < threadHeaders.Count; i++, currentIndex++)
                        {
                            var header = threadHeaders[currentIndex];
                            requests.Add(new PageFetchRequest(header.Url.ChangePageIndex(header.GetLastPageIndex()),
                                header.ToDescription().ChangePageIndexInDescription(header.GetLastPageIndex())));
                        }
                        var oldestReplyDate = await ProcessThreadPageRequests(requests, expirationDate, fetchQueue, ct);

                        if ((oldestReplyDate != DateTime.MaxValue) && oldestReplyDateTillNow == DateTime.MaxValue || (oldestReplyDateTillNow - oldestReplyDate).TotalHours < 24)//如果最新发现的回复比上一次发现的回复晚一天之内是正常的，否则说明有删帖，忽视该异常回复即可
                        {
                            oldestReplyDateTillNow = oldestReplyDate;
                            if (oldestReplyDate < expirationDate)
                            {
                                //以下的肯定是更旧的贴，无需再看了，跳出while循环，处理fetchQueue。
                                //但有时因为删帖的原因，一个最后回复非常早的贴会出现在正常列表中，导致提前终止运行。
                                entryPointUrl = null;
                                break;
                            }
                        }
                        //TODO:Delay 
                    } //while
                    if (currentIndex >= threadHeaders.Count)
                    {
                        //curentIndex达到了最大，说明这一页的全部thread都是新的，有必要看下一页
                        entryPointUrl = forumPageMillResult.NextPageUrl;
                    }
                    //开始处理fetchQueue中
                    while (fetchQueue.Count > 0)
                    {
                        var requests = new List<PageFetchRequest>(PageFetchBatchSize);
                        for (var i = 0; i < PageFetchBatchSize && fetchQueue.Count > 0; i++)
                        {
                            requests.Add(fetchQueue.Dequeue());
                        }
                        await ProcessThreadPageRequests(requests, expirationDate, fetchQueue, ct);
                        //TODO:Delay 
                    }
                }
            }
            catch (TaskCanceledException)//CancellationTokenSource is canceled.
            {
                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                Stop();
                watch.Stop();
                runningInfo.IsCompleted = true;
                SaveRunningInfo(runningInfo);
                _outputAction(string.Format("运行完成，耗时 {0} 秒，约{1:0.0}分钟", watch.Elapsed.TotalSeconds, watch.Elapsed.TotalSeconds / 60.0));
            }
        }

        public void Stop()
        {
            _pageSaveJobRunner.Stop();
        }

        private void HandleForumPageMillError(string entryPointUrl, MillStatus processStatus)
        {
            if (processStatus == MillStatus.FormatError)
            {
                _outputAction(string.Format("分析主题列表页结构时发生错误，本系统将停止执行。Url : {0}", entryPointUrl));
                throw new Exception("erorr");//TODO:是否使用自定义的异常？目前来看没什么必要。下同。
            }
            if (processStatus == MillStatus.NotSignedIn)
            {
                _outputAction(string.Format("登录信息失效，本系统将停止执行。将来会支持重新登录后继续运行。Url : {0}", entryPointUrl));
                throw new Exception("erorr");
            }
            if (processStatus == MillStatus.PermissionDenied)
            {
                _outputAction(string.Format("您提供的用户没有访问主题列表的权限，本系统将停止执行。Url : {0}", entryPointUrl));
                throw new Exception("error");
            }
        }

        private async Task<DateTime> ProcessThreadPageRequests(IEnumerable<PageFetchRequest> requests, DateTime expirationDate, Queue<PageFetchRequest> fetchQueue, CancellationToken ct)
        {
            var oldestReplyDate = DateTime.MaxValue;
            var tasks = requests.Select((r, i) => _pageFetcher.Fetch(r, i * DelayInHttpRequests, ct)).ToList();
            while (tasks.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var completedTask = await Task.WhenAny(tasks);
                if (completedTask.Status == TaskStatus.RanToCompletion)
                {
                    if (completedTask.Result.Error == null)
                    {
                        MillResult<ForumThread> threadLastPageProcessResult;
                        var processStatus = _pageProcessor.TryProcessPage(new MillRequest
                        {
                            Url = completedTask.Result.Url,
                            HtmlContent = completedTask.Result.Content,
                            HumanReadableDescription = completedTask.Result.HumanReadableDescription
                        }, out threadLastPageProcessResult);
                        switch (processStatus)
                        {
                            case MillStatus.Success:
                                _outputAction(threadLastPageProcessResult.HumanReadableDescription.ChangeStatusInDescription("处理完成"));
                                var thread = threadLastPageProcessResult.Result;
                                if (thread.Posts != null && thread.Posts.Count > 0)
                                {
                                    _pageSaveJobRunner.EnqueueRange(thread.Posts);
                                    var lastPost = thread.Posts.Last();
                                    var lastReplyDate = lastPost.ModifyDate ?? lastPost.CreateDate;
                                    if (lastReplyDate < oldestReplyDate)
                                    {
                                        oldestReplyDate = lastReplyDate;
                                    }
                                }
                                if (threadLastPageProcessResult.NextPageUrl != null)
                                {
                                    if (threadLastPageProcessResult.NextPageUrl.GetPageIndex() != "1" && thread.Posts != null && thread.Posts.Count > 0)
                                    {
                                        var oldestPost = thread.Posts.First();
                                        if (oldestPost.CreateDate < expirationDate)
                                        {
                                            //本页最旧的reply总是比上一页最新的reply新，所以如果本页最旧reply发表时间不再获取范围内，上一页肯定不在，直接跳到第一页。
                                            //无论如何看一下第一页是有意义的，因为挖坟的人主要针对第一页上的主题贴。
                                            threadLastPageProcessResult.NextPageUrl =
                                                threadLastPageProcessResult.NextPageUrl.ChangePageIndex(1);
                                        }
                                    }
                                    fetchQueue.Enqueue(new PageFetchRequest(
                                        threadLastPageProcessResult.NextPageUrl,
                                        threadLastPageProcessResult.HumanReadableDescription
                                            .ChangePageIndexInDescription(
                                                int.Parse(threadLastPageProcessResult.NextPageUrl.GetPageIndex()))));
                                }
                                break;
                            case MillStatus.FormatError:
                                _outputAction(completedTask.Result.HumanReadableDescription.ChangeStatusInDescription("无法解析"));
                                break;
                            case MillStatus.PermissionDenied:
                                _outputAction(completedTask.Result.HumanReadableDescription.ChangeStatusInDescription("没权限看"));
                                break;
                            case MillStatus.NotSignedIn:
                                _outputAction(completedTask.Result.HumanReadableDescription.ChangeStatusInDescription("无法登录"));
                                break;
                        }
                    }
                    else
                    {//连接错误或超时，重试
                        var result = completedTask.Result;
                        if (result.Error.IsConnectionError)
                        {//可能是连不上网，或者网站服务器关了
                            _outputAction(result.HumanReadableDescription.ChangeStatusInDescription("网络不通"));
                        }
                        else if (result.Error.IsTimeout)
                        {//超时
                            _outputAction(result.HumanReadableDescription.ChangeStatusInDescription("请求超时"));
                        }
                        else if (result.Error.StatusCode != HttpStatusCode.OK)
                        {//服务器返回错误信息
                            _outputAction(result.HumanReadableDescription.ChangeStatusInDescription("请求错误"));
                        }
                        var retryCount = GetRetryCount(result.Url);
                        if (retryCount < ThreadPageMaxRetryTimes)
                        {
                            fetchQueue.Enqueue(result);
                            _retryCounter[result.Url] = retryCount + 1;
                        }
                        else
                        {
                            Logger.Error("获取URL:{0}重试次数达到上限{1}，不再继续尝试。", result.Url, ThreadPageMaxRetryTimes);
                            _outputAction(result.HumanReadableDescription.ChangeStatusInDescription("不再重试"));
                            _retryCounter.Remove(completedTask.Result.Url);
                        }
                    }
                }
                else if (completedTask.Status == TaskStatus.Canceled)
                {
                    ct.ThrowIfCancellationRequested();
                }
                else
                {
                    if (completedTask.Exception != null)
                    {
                        Logger.Error(completedTask.Exception);
                    }
                }
                tasks.Remove(completedTask);
            }
            return oldestReplyDate;
        }

        private async Task<string> FetchPageContent(string url, int maxRetryTimes, CancellationToken ct)
        {
            var retryInterval = InitialRetryInterval;
            PageFetchResult result;
            while ((result = await _pageFetcher.Fetch(new PageFetchRequest(url),ct)).Content == null)
            {
                var retryCount = GetRetryCount(url);
                if (retryCount + 1 >= maxRetryTimes)
                {
                    _retryCounter.Remove(url);
                    return null;
                }
                _retryCounter[url] = retryInterval + 1;

                await Task.Delay(TimeSpan.FromMilliseconds(retryInterval), ct);
                retryInterval *= 2;
                if (retryInterval > MaxRetryInterval)
                    retryInterval = InitialRetryInterval;
            }

            return result.Content;
        }

        private int GetRetryCount(string url)
        {
            return _retryCounter.ContainsKey(url) ? _retryCounter[url] : 0;
        }

        private void SaveRunningInfo(RunningInfo running)
        {
            running.LastSavedTime = DateTime.Now;
            _runningInfoRepository.SaveAsync(running);
        }
    }

    #region static extensions
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
            if (header.ReplyCount == -1)//somehow replycount is unknown
                return 9999;

            const int postsPerPage = 30;
            return header.ReplyCount / postsPerPage + 1;
        }

        public static string ToDescription(this ThreadHeader header)
        {
            var pageIndex = header.Url.GetPageIndex();
            return string.Format("第{0, 4}页\t　　　　\t{1}", pageIndex, header.Title.ToFixedLength(20));
        }

        public static string ChangePageIndexInDescription(this string desc, int newPageIndex)
        {
            var re = new Regex(@"^第[ \d]{4}页");
            return re.Replace(desc, string.Format("第{0, 4}页", newPageIndex));
        }

        public static string ChangeStatusInDescription(this string desc, string status)
        {
            var statusRex = new Regex("\t.{4}\t");
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
    #endregion
}
