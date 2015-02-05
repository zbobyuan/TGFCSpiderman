using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    class PendingUpdateElement : ConfigurationElement
    {
        [ConfigurationProperty("dir")]
        public string Dir
        {
            get { return (string)this["dir"]; }
            set { this["dir"] = value; }
        }

        [ConfigurationProperty("ver")]
        public string Ver
        {
            get { return (string)this["ver"]; }
            set { this["ver"] = value; }
        }
    }
}
