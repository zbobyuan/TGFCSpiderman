using System.Windows;
using taiyuanhitech.TGFCSpiderman;

namespace taiyuanhitech.TGFCSpidermanX
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ComponentFactory.Startup();
        }
    }
}
