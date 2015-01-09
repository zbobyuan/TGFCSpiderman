using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpiderman.Configuration
{
    public interface IConfiguration
    {

    }

    public interface IPageFetcherConfig
    {
        int SigninRetryTimes { get; set; }
        int TimeoutInSeconds { get; set; }
        string UserAgent { get; set; }
    }

    public interface IAuthConfig
    {
        string UserName { get; set; }
        string AuthToken { get; set; }
    }
}
