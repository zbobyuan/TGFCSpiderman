using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
