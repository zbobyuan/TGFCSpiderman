using System.Windows;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpidermanX
{
    public partial class App
    {
        public RunningInfo RunningInfo { get; set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ComponentFactory.Startup();
        }
    }
}
