using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public class PageFetchJobRunner : JobRunner<PageFetchRequest>
    {
        private const int WaitingMultiplier = 0;//TODO:configurable
        private readonly IPageFetcher _pageFetcher;
        private int _lastTimeRunningElapsedMilliseconds;

        public PageFetchJobRunner(IPageFetcher fetcher)
        {
            _pageFetcher = fetcher;
        }

        protected override int BatchSize
        {
            get { return 3; }//TODO:configurable
        }

        protected override void ExecuteJobs(List<PageFetchRequest> jobs)
        {
            Console.WriteLine("等待{0:0.00}秒", _lastTimeRunningElapsedMilliseconds * WaitingMultiplier / 1000.00);
            Thread.Sleep(_lastTimeRunningElapsedMilliseconds * WaitingMultiplier);
            jobs.ForEach(j => Console.WriteLine(j.HumanReadableDescription.ChangeStatusInDescription("正在获取")));
            var sw = Stopwatch.StartNew();
            var tasks = jobs.Select(job => _pageFetcher.Fetch(job)
                            .ContinueWith(task => TaskQueueManager.Inst.OnPageFetchCompleted(task.Result))).ToArray();
            Task.WaitAll(tasks);
            sw.Stop();
            _lastTimeRunningElapsedMilliseconds = (int)sw.ElapsedMilliseconds;
        }
    }
}