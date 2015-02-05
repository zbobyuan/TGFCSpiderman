using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpiderman
{
    public partial class App
    {
        static readonly Mutex AppMutex = new Mutex(true, "[E0F06A0A-1399-4E86-ACC6-C48973F2B854}");

        public static App CurrentApp
        {
            get
            {
                return Current as App;
            }
        }

        public RunningInfo RunningInfo { get; set; }

        public bool IsSignedin
        {
            get
            {
                return ComponentFactory.GetPageFetcher().HasAuthToken;
            }
        }

        internal OnlineUpdate.UpdateInfo NewUpdateInfo { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!AppMutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("已经有一个在运行了，不能多开。");
                Current.Shutdown();
            }
            var config = new ConfigurationManager();
            var pendingUpdate = config.GetPendingUpdateElement();
            if (pendingUpdate != null && !string.IsNullOrEmpty(pendingUpdate.Dir))
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        FileName = "TGSUpdater.exe",
                        Arguments = string.Format("\"{0}\" {1}", pendingUpdate.Dir, pendingUpdate.Ver),
                        UseShellExecute = true
                    }
                };
                config.SavePendingUpdate("","");
                p.Start();
                Current.Shutdown();
                return;
            }
            base.OnStartup(e);
            ComponentFactory.Startup();
        }
    }
}