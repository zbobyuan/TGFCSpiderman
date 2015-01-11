using System;
using System.Collections.Generic;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public interface IPageProcessor
    {
        MillResult<List<ThreadHeader>> ProcessForumPage(MillRequest request);
        MillResult<ForumThread> ProcessThreadPage(MillRequest request);
    }

    public abstract class PageMillException : Exception
    {
        protected PageMillException(MillRequest request, string message, Exception e)
            : base(message, e)
        {
            Request = request;
        }

        public MillRequest Request { get; private set; }
    }

    public class NotSignedInException : PageMillException
    {
        public NotSignedInException(MillRequest request)
            : base(request, "", null)
        {
        }
    }

    public class ProcessFaultException : PageMillException
    {
        public ProcessFaultException(MillRequest request, string message, Exception e = null)
            : base(request, message, e)
        {
        }
    }

    public class PermissionDeniedException : PageMillException
    {
        public PermissionDeniedException(MillRequest request)
            : base(request, "", null)
        {
        }
    }
}