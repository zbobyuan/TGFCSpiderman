using System;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public interface IPageFetcher
    {
        Task<PageFetchResult> Fetch(PageFetchRequest request);
        Task<string> Signin(string userName, string password);
        bool HasAuthToken { get; }
    }

    public class CannotSigninException : Exception
    {
        public CannotSigninException(string message)
            : base(message)
        {
        }
    }
}