using System;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public interface IPageFetcher
    {
        Task<PageFetchResult> Fetch(PageFetchRequest request);
        Task Signin(string userName, string password);
    }

    public class CannotSigninException : Exception
    {
    }

    public class UserNameOrPasswordException : CannotSigninException
    {
    }
}