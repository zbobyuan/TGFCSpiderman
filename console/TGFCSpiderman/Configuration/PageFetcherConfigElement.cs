using System.Configuration;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class PageFetcherConfigElement : ConfigurationElement, IPageFetcherConfig
    {
        public PageFetcherConfigElement()
        {
        }

        public PageFetcherConfigElement(int signinRetryTimes, int timeoutInSeconds, string userAgent)
        {
            SigninRetryTimes = signinRetryTimes;
            TimeoutInSeconds = timeoutInSeconds;
            UserAgent = userAgent;
        }

        [ConfigurationProperty("signinRetryTimes", DefaultValue = 3, IsRequired = false)]
        public int SigninRetryTimes
        {
            get { return (int)this["signinRetryTimes"]; }
            set { this["signinRetryTimes"] = value; }
        }

        [ConfigurationProperty("timeout", DefaultValue = 20, IsRequired = false)]
        public int TimeoutInSeconds
        {
            get { return (int)this["timeout"]; }
            set { this["timeout"] = value; }
        }

        [ConfigurationProperty("user-agent", IsRequired = false)]
        public string UserAgent
        {
            get { return (string)this["user-agent"]; }
            set { this["user-agent"] = value; }
        }
    }
}
