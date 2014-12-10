using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public class PageFetchJobRunner : JobRunner<PageFetchRequest>
    {
        private readonly IPageFetcher _pageFetcher;

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
            jobs.ForEach(j => Console.WriteLine(j.HumanReadableDescription.ChangeStatusInDescription("正在获取")));
            var tasks = jobs.Select(job => _pageFetcher.Fetch(job)
                            .ContinueWith(task => TaskQueueManager.Inst.OnPageFetchCompleted(task.Result))).ToArray();
            Task.WaitAll(tasks);
        }
    }
}