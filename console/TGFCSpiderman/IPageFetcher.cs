using System;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public interface IPageFetcher
    {
        Task<PageFetchResult> Fetch(PageFetchRequest request);
        Task<PageFetchResult> Fetch(PageFetchRequest request, int delayInMilliseconds);
        Task<string> Signin(string userName, string password);
        void Signout();
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