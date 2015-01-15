using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class PageFetchRequest
    {
        public PageFetchRequest(string url, string desc = null)
        {
            Url = url;
            HumanReadableDescription = desc;
        }

        public string Url { get; set; }
        public string HumanReadableDescription { get; set; }
    }
}
