using System;
using taiyuanhitech.TGFCSpiderman.IoC;
using taiyuanhitech.TGFCSpiderman.Persistence;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpiderman
{
    public static class ComponentFactory
    {
        public static IoCContainer Container = new IoCContainer();

        public static void Startup(Func<IPageFetcher> pageFetcherCreator, Func<IPageProcessor> pageProcessorCreator,
            Func<IPostRepository> postRepositoryCreator, Func<IRunningInfoRepository> runningInfoRepositoryCreator)
        {
            Container.Register<IPageFetcher>(pageFetcherCreator).
                Register<IPageProcessor>(pageProcessorCreator).
                Register<IPostRepository>(postRepositoryCreator).
                Register<IRunningInfoRepository>(runningInfoRepositoryCreator);

            _pageFetcher = Container.Create<IPageFetcher>();
            _pageProcessor = Container.Create<IPageProcessor>();
            _postRepository = Container.Create<IPostRepository>();
            _runningInfoRepository = Container.Create<IRunningInfoRepository>();
        }

        public static void Startup()
        {
            var cm = new ConfigurationManager();

            Startup(() => new PageFetcher(cm.GetPageFetcherConfig(), cm.GetAuthConfig().AuthToken), 
                () => new PageProcessor(), 
                () => new PostRepository(),
                () => new RunningInfoRepository());
        }

        private static IPageFetcher _pageFetcher;
        private static IPageProcessor _pageProcessor;
        private static IPostRepository _postRepository;
        private static IRunningInfoRepository _runningInfoRepository;

        public static IPageFetcher GetPageFetcher()
        {
            return _pageFetcher;
        }

        public static IPageProcessor GetPageProcessor()
        {
            return _pageProcessor;
        }

        public static IPostRepository GetPostRepository()
        {
            return _postRepository;
        }

        public static IRunningInfoRepository GetRunningInfoRepository()
        {
            return _runningInfoRepository;
        }
    }
}