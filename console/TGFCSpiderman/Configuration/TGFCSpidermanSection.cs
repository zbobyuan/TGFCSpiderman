using System.Configuration;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class TGFCSpidermanSection : ConfigurationSection
    {
        [ConfigurationProperty("pageFetcher")]
        public PageFetcherConfigElement PageFetcherElement
        {
            get { return (PageFetcherConfigElement) this["pageFetcher"]; }
            set { this["pageFetcher"] = value; }
        }
        [ConfigurationProperty("girls")]
        public AuthConfigElement AuthElement
        {
            get { return (AuthConfigElement)this["girls"]; }
            set { this["girls"] = value; }
        }
        [ConfigurationProperty("onlineUpdate")]
        public OnlineUpdateConfigElement OnlineUpdateElement
        {
            get { return (OnlineUpdateConfigElement)this["onlineUpdate"];}
            set { this["onlineUpdate"] = value; }
        }
    }
}
