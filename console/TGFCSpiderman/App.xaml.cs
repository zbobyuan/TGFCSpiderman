using System.Threading;
using System.Windows;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public partial class App
    {
        static readonly Mutex AppMutex = new Mutex(true, "[E0F06A0A-1399-4E86-ACC6-C48973F2B854}");
        public RunningInfo RunningInfo { get; set; }

        public bool IsSignedin
        {
            get
            {
                return ComponentFactory.GetPageFetcher().HasAuthToken;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            //if (!AppMutex.WaitOne(TimeSpan.Zero, true))
            //{
            //    MessageBox.Show("已经有一个在运行了，不能多开。");
            //    Current.Shutdown();
            //}
            base.OnStartup(e);
            ComponentFactory.Startup();
        }
    }
}