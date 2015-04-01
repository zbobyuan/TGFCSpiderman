using System.Windows;

namespace taiyuanhitech.Updater
{
    public partial class App
    {
        public string Dir { get; set; }
        public string Ver { get; set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length != 2)
            {
                AlertAndExit();
            }
            else
            {
                Dir = e.Args[0];
                Ver = e.Args[1];
            }
        }

        private static void AlertAndExit()
        {
            MessageBox.Show("不要直接点我。","TGFCSpiderman更新",MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }
    }
}
