using System.Net;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class PageFetchResult : PageFetchRequest
    {
        public class ErrorInfo
        {
            public bool IsConnectionError { get; set; }
            public bool IsTimeout { get; set; }
            public HttpStatusCode StatusCode { get; set; }
        }

        public PageFetchResult(PageFetchRequest request)
            : base(request.Url, request.HumanReadableDescription)
        {
        }
        public string Content { get; set; }
        public ErrorInfo Error { get; set; }
    }
}