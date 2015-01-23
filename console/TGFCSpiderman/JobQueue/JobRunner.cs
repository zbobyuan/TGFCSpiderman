using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NLog;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public abstract class JobRunner<TRequest>
        where TRequest : class
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private BlockingCollection<TRequest> _jobQueue;
        private Thread _worker;

        protected virtual int BatchSize
        {
            get { return 1; }
        }

        public void Run()
        {
            if (_worker != null)
            {
                throw new InvalidOperationException("Cannot run a runner whick is running.");
            }
            _jobQueue = new BlockingCollection<TRequest>();
            _worker = new Thread(DoWork) ;
            _worker.Start();
        }

        public void EnqueueJob(TRequest job)
        {
            _jobQueue.Add(job);
        }

        public void EnqueueRange(IEnumerable<TRequest> jobs)
        {
            foreach (var job in jobs)
            {
                EnqueueJob(job);
            }
        }

        public void Stop()
        {
            _jobQueue.CompleteAdding();
        }
        protected abstract void ExecuteJobs(List<TRequest> jobs);

        protected void DoWork()
        {
            var jobs = new List<TRequest>(BatchSize);
            var currentIndex = 0;
            foreach (var job in _jobQueue.GetConsumingEnumerable())
            {
                jobs.Add(job);
                currentIndex++;
                if (currentIndex == BatchSize)
                {
                    ExecuteJobs2(jobs);
                    currentIndex = 0;
                    jobs.Clear();
                }
            }
            if (jobs.Count > 0)
            {
                ExecuteJobs2(jobs);
            }
        }

        private void ExecuteJobs2(List<TRequest> jobs)
        {
            try
            {
                ExecuteJobs(jobs);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}