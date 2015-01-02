using System;
using System.Collections.Generic;
using NLog;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public static class MillPageHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static MillStatus TryProcessPage<T>(this IPageProcessor pageProcessor, MillRequest request, out MillResult<T> result)
            where T : class
        {
            result = new MillResult<T>{ Url = request.Url, HumanReadableDescription = request.HumanReadableDescription};
            try
            {
                if (typeof (T) == typeof (List<ThreadHeader>))
                {
                    result = pageProcessor.ProcessForumPage(request) as MillResult<T>;
                }
                else if (typeof (T) == typeof (ForumThread))
                {
                    result = pageProcessor.ProcessThreadPage(request) as MillResult<T>;
                }
                else
                {
                    throw new NotSupportedException();
                }
                return MillStatus.Success;
            }
            catch (NotSignedInException ne)
            {
                Logger.Trace("解析网页时发现未登录，URL: {0}\r\n 内容:{1}", ne.Request.Url, ne.Request.HtmlContent);
                return MillStatus.NotSignedIn;
            }
            catch (PermissionDeniedException pde)
            {
                Logger.Trace("解析网页时发现没有权限，URL: {0}\r\n 内容:{1}", pde.Request.Url, pde.Request.HtmlContent);
                return MillStatus.PermissionDenied;
            }
            catch (ProcessFaultException pfe)
            {
                Logger.Trace("解析网页时发生错误，错误信息：{0}\r\n URL: {1}\r\n 内容:{2}\r\n 内部异常:{3}\r\n", pfe.Message, pfe.Request.Url,
                    pfe.Request.HtmlContent, pfe.InnerException);
                return MillStatus.FormatError;
            }
            catch (Exception e)
            {
                Logger.Trace("Error URL:{0}\r\n{1}",request.Url, e);
                return MillStatus.FormatError;
            }
        }
    }
}