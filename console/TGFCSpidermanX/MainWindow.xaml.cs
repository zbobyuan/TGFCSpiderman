using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.Configuration;
using taiyuanhitech.TGFCSpidermanX.ViewModel;

namespace taiyuanhitech.TGFCSpidermanX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly ConfigurationManager _configurationManager = new ConfigurationManager();
        private readonly Dashboard _dashboardViewModel;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardViewModel = (Dashboard) FindResource("DashboardViewModel");
            var authConfig = _configurationManager.GetAuthConfig();
            _dashboardViewModel.UserName = authConfig.UserName;

            if (string.IsNullOrEmpty(_dashboardViewModel.UserName))
            {
                _dashboardViewModel.LoginInfoEnabled = true;
            }
            else
            {
                Password.Password = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                Login.Content = "重新登陆";
            }
        }

        private async void Signin_OnClick(object sender, RoutedEventArgs e)
        {
            if (_dashboardViewModel.LoginInfoEnabled)
            {
                Login.IsEnabled = false;
                var pageFetcher = ComponentFactory.GetPageFetcher();
                try
                {
                    var name = _dashboardViewModel.UserName;
                    var password = Password.Password;
                    var authToken = await pageFetcher.Signin(name, password);
                    _configurationManager.SaveAuthConfig(new AuthConfig {UserName = name, AuthToken = authToken});
                    Password.Password = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                    _dashboardViewModel.LoginInfoEnabled = false;
                    Login.Content = "重新登录";
                }
                catch (CannotSigninException cse)
                {
                    MessageBox.Show(string.IsNullOrEmpty(cse.Message) ? "登录不了，可能是网络不行。" : cse.Message);
                    return;
                }
                finally
                {
                    Login.IsEnabled = true;
                }
            }
            else
            {
                _dashboardViewModel.LoginInfoEnabled = true;
                Password.Clear();
                Login.Content = "登录";
            }
        }

        private async void Run_OnClick(object sender, RoutedEventArgs e)
        {
            //OutputBox.AppendText(DateTime.Now + Environment.NewLine);
            //OutputBox.ScrollToEnd();
            Run.IsEnabled = false;
            if (!ComponentFactory.GetPageFetcher().HasAuthToken)
            {
                MessageBox.Show("先登录。");
                return;
            }
            await Task.Run(()=>
            TaskQueueManager.Inst.Run(DateTime.Now.AddDays(-1), s =>
            {
                if (System.Threading.Thread.CurrentThread == OutputBox.Dispatcher.Thread)
                {
                    OutputBox.AppendText(s + Environment.NewLine);
                    OutputBox.ScrollToEnd();
                }
                else
                {
                    OutputBox.Dispatcher.InvokeAsync(() =>
                    {
                        OutputBox.AppendText(s + Environment.NewLine);
                        OutputBox.ScrollToEnd();
                    });
                }
            }));
            Run.IsEnabled = true;
        }
    }
    class AuthConfig : IAuthConfig
    {
        public string UserName { get; set; }
        public string AuthToken { get; set; }
    }
}
