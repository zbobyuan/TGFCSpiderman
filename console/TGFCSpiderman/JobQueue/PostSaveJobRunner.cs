using System.Collections.Generic;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Persistence;

namespace taiyuanhitech.TGFCSpiderman.JobQueue
{
    public class PostSaveJobRunner : JobRunner<Post>
    {
        private readonly IPostRepository _repository;

        public PostSaveJobRunner(IPostRepository repos)
        {
            _repository = repos;
        }

        protected override int BatchSize
        {
            get { return 10; }//TODO:configurable
        }

        protected override void ExecuteJobs(List<Post> jobs)
        {
            _repository.SavePosts(jobs);
        }
    }
}