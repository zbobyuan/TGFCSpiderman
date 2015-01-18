using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using NLog;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Configuration;
using taiyuanhitech.TGFCSpidermanX.ViewModel;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpidermanX
{
    public partial class MainWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly App _app;
        private readonly ConfigurationManager _configurationManager = new ConfigurationManager();
        private readonly Dashboard _dashboardViewModel;
        private readonly SearchViewModel _searchViewModel;
        private CancellationTokenSource _cts;

        public MainWindow()
        {
            _app = (App)Application.Current;
            InitializeComponent();
            _dashboardViewModel = (Dashboard)FindResource("DashboardViewModel");
            _searchViewModel = (SearchViewModel)FindResource("SearchViewModel");
            var authConfig = _configurationManager.GetAuthConfig();
            _dashboardViewModel.UserName = authConfig.UserName;
            ExpirationDate.SelectedDate = DateTime.Now.AddDays(-1);

            if (string.IsNullOrEmpty(_dashboardViewModel.UserName))
            {
                _dashboardViewModel.LoginInfoEnabled = true;
                UserNameBox.Focus();
            }
            else if (string.IsNullOrEmpty(authConfig.AuthToken))
            {
                _dashboardViewModel.LoginInfoEnabled = true;
                Password.Focus();
            }
            else
            {
                Password.Password = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                Login.Content = "退出";
            }
        }

        private async void Signin_OnClick(object sender, RoutedEventArgs e)
        {
            if (_dashboardViewModel.LoginInfoEnabled)
            {
                Login.IsEnabled = false;
                _dashboardViewModel.LoginInfoEnabled = false;

                var pageFetcher = ComponentFactory.GetPageFetcher();
                var isSignedIn = false;
                try
                {
                    var name = _dashboardViewModel.UserName;
                    var password = Password.Password;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password))
                    {
                        //TODO:Use wpf data validation
                        MessageBox.Show("别瞎输、瞎点。");
                        return;
                    }
                    SigninProgress.Visibility = Visibility.Visible;
                    var authToken = await pageFetcher.Signin(name, password);
                    isSignedIn = true;
                    _configurationManager.SaveAuthConfig(new AuthConfig { UserName = name, AuthToken = authToken });
                    Password.Password = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                    _dashboardViewModel.LoginInfoEnabled = false;
                    Login.Content = "退出";
                }
                catch (CannotSigninException cse)
                {
                    MessageBox.Show(string.IsNullOrEmpty(cse.Message) ? "登录不了，可能是网络不行。" : cse.Message);
                }
                finally
                {
                    Login.IsEnabled = true;
                    SigninProgress.Visibility = Visibility.Hidden;
                    if (!isSignedIn)
                    {
                        _dashboardViewModel.LoginInfoEnabled = true;
                        if (string.IsNullOrWhiteSpace(_dashboardViewModel.UserName))
                        {
                            UserNameBox.Focus();
                        }
                        else
                        {
                            Password.Focus();
                        }
                    }
                }
            }
            else
            {
                ComponentFactory.GetPageFetcher().Signout();
                _configurationManager.SaveAuthConfig(new AuthConfig { UserName = _dashboardViewModel.UserName, AuthToken = "" });
                _dashboardViewModel.LoginInfoEnabled = true;
                Password.Clear();
                Password.Focus();
                Login.Content = "登录";
            }
        }

        private async void Run_OnClick(object sender, RoutedEventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                var newCts = new CancellationTokenSource();
                _cts = newCts;
                return;
            }
            if (!ComponentFactory.GetPageFetcher().HasAuthToken)
            {
                MessageBox.Show("先登录。");
                Run.Content = "运行";
                return;
            }
            Run.Content = "取消";
            RunProgress.Visibility = Visibility.Visible;
            Login.IsEnabled = false;
            OutputBox.Clear();
            var outputCount = 0;
            var taskManager = new TaskQueueManager(ComponentFactory.GetPageFetcher(),
                ComponentFactory.GetPageProcessor(), s =>
                {
                    Action output = () =>
                        {
                            if (outputCount++ > 200)
                            {
                                OutputBox.Clear();
                                outputCount = 0;
                            }
                            OutputBox.AppendText(s + Environment.NewLine);
                            OutputBox.ScrollToEnd();
                        };
                    if (Thread.CurrentThread == OutputBox.Dispatcher.Thread)
                    {
                        output();
                    }
                    else
                    {
                        OutputBox.Dispatcher.InvokeAsync(output);
                    }
                });

            _cts = new CancellationTokenSource();
            _app.RunningInfo = await ComponentFactory.GetRunningInfoRepository().GetLastUncompletedAsync();
            if (_app.RunningInfo != null)
            {
                var runLastUncompletedTask = MessageBox.Show(string.Format("上次开始于{0}的任务没运行完成，要继续吗？\r\n选择\"是\"继续运行，选择\"否\"开始运行新任务。", _app.RunningInfo.StartTime),
                    "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                if (!runLastUncompletedTask)
                {
                    _app.RunningInfo.IsCompleted = true;
                    _app.RunningInfo.LastSavedTime = DateTime.Now;
                    await ComponentFactory.GetRunningInfoRepository().SaveAsync(_app.RunningInfo);
                    _app.RunningInfo = null;
                }
            }
            if (_app.RunningInfo == null)
            {
                var date = GetExpirationDate();
                _app.RunningInfo = new RunningInfo
                {
                    InitialEntryPointUrl = GetEntryPointUrl(),
                    InitialExpirationDate = date,
                    CurrentExpirationDate = date,
                    Mode = GetRunningMode(),
                    StartTime = DateTime.Now,
                };
            }

            try
            {
                await taskManager.Run(_app.RunningInfo, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("已取消。");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                MessageBox.Show(ex.Message);
            }
            finally
            {
                _app.RunningInfo = null;
            }
            Run.Content = "运行";
            RunProgress.Visibility = Visibility.Hidden;
            Login.IsEnabled = true;
            _cts = null;
        }

        private DateTime GetExpirationDate()
        {
            var dateText = ExpirationDate.Text;
            DateTime date;
            if (!DateTime.TryParse(dateText, out date))
            {
                date = ExpirationDate.SelectedDate ?? DateTime.Now.AddDays(-1);
            }
            return date;
        }

        private RunningInfo.RunningMode GetRunningMode()
        {
            return SignleMode.IsChecked ?? true ? RunningInfo.RunningMode.Single : RunningInfo.RunningMode.Cycle;
        }

        private string GetEntryPointUrl()
        {//TODO:告知用户循环模式下不能指定开始页码
            var mode = GetRunningMode();
            if (mode != RunningInfo.RunningMode.Single)
                return "index.php?action=forum&fid=25&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default&page=1";
            var startPageText = StartPageBox.Text;
            int startPage;
            if (!int.TryParse(startPageText, out startPage))
                startPage = 1;
            return "index.php?action=forum&fid=25&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default&page=" + startPage;
        }

        private void SigninInput_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Signin_OnClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async void Search_OnClick(object sender, RoutedEventArgs e)
        {
            SearchProgress.Visibility = Visibility.Visible;
            var repos = ComponentFactory.GetPostRepository();
            var endDate = _searchViewModel.EndDate;
            if (endDate != null)
            {
                endDate = endDate.Value.AddSeconds(24 * 60 * 60 - 1);
            }
            var result = await repos.SearchAsync(_searchViewModel.UserName, _searchViewModel.Title, _searchViewModel.Content, 
                _searchViewModel.StartDate, endDate, _searchViewModel.TopicOnly, 10, 1);

            SearchGrid.ItemsSource = result;
            SearchProgress.Visibility = Visibility.Hidden;
        }
    }

    internal class AuthConfig : IAuthConfig
    {
        public string UserName { get; set; }
        public string AuthToken { get; set; }
    }
}