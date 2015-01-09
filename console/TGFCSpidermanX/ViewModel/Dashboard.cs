using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpidermanX.ViewModel
{
    public class Dashboard : INotifyPropertyChanged
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
        {//TODO:is it thread safe?
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

}
