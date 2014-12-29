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
            Func<IPostRepository> postRepositoryCreator)
        {
            Container.Register<IPageFetcher>(pageFetcherCreator).
                Register<IPageProcessor>(pageProcessorCreator).
                Register<IPostRepository>(postRepositoryCreator);

            _pageFetcher = Container.Create<IPageFetcher>();
            _pageProcessor = Container.Create<IPageProcessor>();
            _postRepository = Container.Create<IPostRepository>();
        }

        public static void Startup()
        {
            Startup(() => new PageFetcher(new ConfigurationManager().GetPageFetcherConfig()), () => new PageProcessor(), () => new PostRepository());
        }

        private static IPageFetcher _pageFetcher;
        private static IPageProcessor _pageProcessor;
        private static IPostRepository _postRepository;

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
    }
}