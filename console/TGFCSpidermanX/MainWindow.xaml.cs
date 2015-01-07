using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MahApps.Metro.Controls;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpidermanX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private readonly BindingObject _bind;
        private string _userName;
        private string _password;
        public class BindingObject : INotifyPropertyChanged
        {
            private string _userName;
            private bool _loginInfoEnabled;

            public string UserName
            {
                get { return _userName; }
                set
                {
                    if (value == _userName)
                        return;
                    _userName = value;
                    OnPropertyChanged("UserName");
                }
            }

            public bool LoginInfoEnabled
            {
                get { return _loginInfoEnabled; }
                set
                {
                    if (value == _loginInfoEnabled)
                        return;
                    _loginInfoEnabled = value;
                    OnPropertyChanged("LoginInfoEnabled");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            var configurationManager = new ConfigurationManager();
            var authConfig = configurationManager.GetAuthConfig();
            _userName = authConfig.UserName;
            _password = authConfig.Password;
            _bind = new BindingObject
            {
                UserName = authConfig.UserName,
            };
            DataContext = _bind;

            _bind.LoginInfoEnabled = string.IsNullOrEmpty(_userName);
            if (!_bind.LoginInfoEnabled)
            {
                Password.Password = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            }
        }

        private async void Signin_OnClick(object sender, RoutedEventArgs e)
        {
            var pageFetcher = ComponentFactory.GetPageFetcher();
            try
            {
                await pageFetcher.Signin(_bind.UserName, Password.Password).ConfigureAwait(false);
            }
            catch (UserNameOrPasswordException)
            {
                MessageBox.Show("用户密码错误.");
                return;
            }
            catch (CannotSigninException)
            {
                MessageBox.Show("登陆不了，可能是网络不行。");
                return;
            }

            MessageBox.Show("恭喜你登陆成功");
        }
    }
}
