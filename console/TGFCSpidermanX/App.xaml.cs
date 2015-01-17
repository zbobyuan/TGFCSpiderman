using System;
using System.Threading;
using System.Windows;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpidermanX
{
    public partial class App
    {
        static readonly Mutex AppMutex = new Mutex(true, "{991D79D4-6DC1-4FD4-A5C5-C3DB3B17F237}");
        public RunningInfo RunningInfo { get; set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            if (!AppMutex.WaitOne(TimeSpan.Zero, true)) 
            {
                MessageBox.Show("已经有一个在运行了，不能多开。");
                Current.Shutdown();
            }
            base.OnStartup(e);
            ComponentFactory.Startup();
        }
    }
}