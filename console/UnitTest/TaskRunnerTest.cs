using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using taiyuanhitech.TGFCSpiderman.JobQueue;

namespace UnitTest
{
    [TestClass]
    public class JobRunnerTest
    {
        #region mock runner

        private class MockJobRunner : JobRunner<string>
        {
            private readonly int _batchSize;

            public MockJobRunner(int batchSize = 1)
            {
                Result = "";
                _batchSize = batchSize;
            }

            public string Result { get; set; }

            protected override int BatchSize
            {
                get { return _batchSize; }
            }

            protected override void ExecuteJobs(List<string> jobs)
            {
                Console.WriteLine("tasks length:{0},tasks:[{1}]", jobs.Count, string.Join(",", jobs.ToArray()));
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
                jobs.ForEach(s => Result += s);
            }
        }

        #endregion

        [TestMethod]
        public void TaskRunnerBasicTest()
        {
            string[] strs = {"one", "two", "three", "four", "five", "six", "seven"};

            var runner = new MockJobRunner(3);
            runner.Run();
            strs.ToList().ForEach(runner.EnqueueJob);
            runner.IdleWaitHandle.WaitOne();
            Assert.AreEqual(runner.Result, string.Join("", strs));
        }

        [TestMethod]
        public void TaskRunnerCanReEnter()
        {
            string[] strs = {"one", "two", "three", "four", "five", "six", "seven"};

            var runner = new MockJobRunner(3);
            runner.Run();
            strs.ToList().ForEach(runner.EnqueueJob);
            runner.IdleWaitHandle.WaitOne();

            runner.Result = "";
            Console.WriteLine();

            strs.Take(2).ToList().ForEach(runner.EnqueueJob);
            Thread.Sleep(TimeSpan.FromMilliseconds(300));
            runner.EnqueueRange(strs.Skip(2));
            runner.IdleWaitHandle.WaitOne();
            Assert.AreEqual(runner.Result, string.Join("", strs));
        }
    }
}