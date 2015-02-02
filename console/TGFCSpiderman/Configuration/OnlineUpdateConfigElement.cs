using System.Configuration;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class OnlineUpdateConfigElement : ConfigurationElement
    {
        internal const string DefaultUrl = "http://localhost/ts/lastest_release.txt";
        [ConfigurationProperty("checkUpdateUrl", IsRequired = false, DefaultValue = DefaultUrl)]
        public string CheckUpdateUrl
        {
            get { return (string)this["checkUpdateUrl"]; }
            set { this["checkUpdateUrl"] = value; }
        }
    }
}