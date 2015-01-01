using System;
using System.Collections.Generic;
using System.Threading;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public abstract class JobRunner<TRequest>
        where TRequest : class
    {
        private readonly ManualResetEvent _idleWaitHandle = new ManualResetEvent(false);
        private readonly AutoResetEvent _jobAvailableWaitHandle = new AutoResetEvent(false);
        private readonly Queue<TRequest> _jobQueue = new Queue<TRequest>();
        private readonly object _queueLocker = new object();
        private readonly Thread _worker;
        protected bool Shutdown;
        private bool _running;

        protected JobRunner()
        {
            _worker = new Thread(DoWork) {IsBackground = true};
        }

        public WaitHandle IdleWaitHandle
        {
            get { return _idleWaitHandle; }
        }

        protected virtual int BatchSize
        {
            get { return 1; }
        }

        public void Run()
        {
            _worker.Start();
            _running = true;
        }

        public void EnqueueJob(TRequest job)
        {
            EnqueueRange(FromSingle(job));
        }

        public void EnqueueRange(IEnumerable<TRequest> jobs)
        {
            if (Shutdown) return;
            if (_running) _idleWaitHandle.Reset();
            lock (_queueLocker)
            {
                foreach (var j in jobs)
                {
                    _jobQueue.Enqueue(j);
                }
            }
            _jobAvailableWaitHandle.Set();
        }

        public void Stop()
        {
            _running = false;
            Shutdown = true;
            _jobAvailableWaitHandle.Set();
        }
        protected abstract void ExecuteJobs(List<TRequest> jobs);

        protected void DoWork()
        {
            while ((!Shutdown))
            {
                List<TRequest> jobs = null;
                lock (_queueLocker)
                {
                    if (_jobQueue.Count > 0)
                    {
                        jobs = new List<TRequest>(BatchSize);
                        while (_jobQueue.Count > 0 && jobs.Count < BatchSize)
                        {
                            jobs.Add(_jobQueue.Dequeue());
                        }
                    }
                }

                if (jobs == null)
                {
                    _idleWaitHandle.Set();
                    _jobAvailableWaitHandle.WaitOne();
                }
                else
                {
                    _idleWaitHandle.Reset();
                    try
                    {
                        ExecuteJobs(jobs);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("未知错误:{0}", e);
                    }
                }
            }
            _idleWaitHandle.Set();
        }

        private static IEnumerable<T> FromSingle<T>(T o)
        {
            yield return o;
        }
    }
}