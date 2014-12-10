using System;
using System.Collections.Generic;
using System.Linq;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public class PageMillJobRunner : JobRunner<MillRequest>
    {
        private readonly IPageProcessor _pageProcessor;

        public PageMillJobRunner(IPageProcessor p)
        {
            _pageProcessor = p;
        }

        protected override void ExecuteJobs(List<MillRequest> jobs)
        {
            var job = jobs.Single();
            var url = job.Url;
            if (url.IndexOf("action=thread", StringComparison.OrdinalIgnoreCase) <= 0) return;
            MillResult<ForumThread> millResult;
            var status = _pageProcessor.TryProcessPage(job, out millResult);
            TaskQueueManager.Inst.OnThreadPageMillCompleted(status, millResult);
        }
    }
}