using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    internal static class ProcessorExt
    {
        private static readonly Regex ThreadIdRegex = new Regex(@"tid=(\d+)");
        private static readonly Regex PostIdRegex = new Regex(@"tid=(\d+)");
        private static readonly Regex PageIndexRegex = new Regex(@"page=(\d+)");

        public static int GetThreadId(this string url)
        {
            Match match = ThreadIdRegex.Match(url);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static int GetPostId(this string url)
        {
            Match match = PostIdRegex.Match(url);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static bool MatchPageIndex(this string url)
        {
            return PageIndexRegex.Match(url).Success;
        }

        public static string GetPageIndex(this string url)
        {
            Match matchs = PageIndexRegex.Match(url);
            return matchs.Success ? matchs.Groups[1].Value : "1";
        }

        public static HtmlNode GetPreviousSibling(this HtmlNode node, string name)
        {
            HtmlNode previous = null, current = node;
            while (current.PreviousSibling != null && (previous = current.PreviousSibling).Name != name)
            {
                current = previous;
            }

            return previous;
        }

        public static HtmlNode GetNextSibling2(this HtmlNode node, string name)
        {//fix GetNextSibling() in ScrapySharp
            try
            {
                return node.GetNextSibling(name);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }
    }

    public class PageProcessor : IPageProcessor
    {
        public MillResult<List<ThreadHeader>> ProcessForumPage(MillRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                throw new ArgumentNullException("request");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(HttpUtility.HtmlDecode(request.HtmlContent));
            var rootNode = htmlDoc.DocumentNode;
            EnsureSignedIn(rootNode, request);

            var titles = rootNode.CssSelect("span.title").ToArray();
            var authors = rootNode.CssSelect("span.author").ToArray();
            if (titles.Length != authors.Length)
                throw new ProcessFaultException(request, "title和author元素个数不一致。");
            if (titles.Length == 0)
                throw new ProcessFaultException(request, "没有找到title和author元素。");

            var headers = new List<ThreadHeader>(titles.Length);

            for (int i = 0; i < titles.Length; i++)
            {
                var titleNode = titles[i];
                var authorNode = authors[i];

                var titleAnchor = titleNode.CssSelect("a").FirstOrDefault();
                if (titleAnchor == null)
                    throw new ProcessFaultException(request, string.Format("第{0}个title元素里面没有a元素。", i));

                var url = titleAnchor.GetAttributeValue("href");
                var titleText = titleAnchor.InnerText.Trim();
                int threadId = url.GetThreadId();
                if (0 == threadId)
                    throw new ProcessFaultException(request, "无法从thread url中获取thread id。");

                var authorText = authorNode.InnerText;
                if (string.IsNullOrWhiteSpace(authorText))
                    throw new ProcessFaultException(request, string.Format("第{0}个author元素没有内容。", i));
                var values = authorText.Split('/');
                if (values.Length != 4)
                    throw new ProcessFaultException(request, string.Format("第{0}个author元素内容经/分割后不是4项。", i));

                var header = new ThreadHeader
                {
                    Id = threadId,
                    Url = url,
                    Title = titleText,
                };
                try
                {
                    header.UserName = values[0].Substring(1);
                    header.ReplyCount = int.Parse(values[1]);
                }
                catch (Exception e)
                {
                    throw new ProcessFaultException(request, string.Format("第{0}个author元素UserName、ReplyCount存在问题。", i), e);
                }
                headers.Add(header);
            }

            return new MillResult<List<ThreadHeader>>
            {
                Url = request.Url,
                NextPageUrl = GetNextForumPageUrl(rootNode, request),
                Result = headers
            };
        }

        public MillResult<ForumThread> ProcessThreadPage(MillRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                throw new ArgumentNullException("request");

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(HttpUtility.HtmlDecode(request.HtmlContent));
            var rootNode = htmlDoc.DocumentNode;
            EnsureSignedIn(rootNode, request);

            var navNode = rootNode.CssSelect("div.wrap > div.navbar").FirstOrDefault();
            if (navNode == null)
                throw new ProcessFaultException(request, "无法定位navbar");

            var bodyNode = navNode.GetNextSibling2("div");
            if (bodyNode == null)
                throw new ProcessFaultException(request, "无法定位包含内容的div元素");

            EnsurePermissionAllowed(bodyNode, request);

            int currentPageIndex = GetThreadPageCurrentIndex(request, rootNode);
            var isFirstPage = currentPageIndex == 1;
            var thread = new ForumThread { Url = request.Url, Id = request.Url.GetThreadId(), CurrentPageIndex = currentPageIndex };

            if (isFirstPage)
            {
                var titleNode = bodyNode.CssSelect("b").First();
                thread.Title = titleNode.InnerText;

                var dateNode = titleNode.GetNextSibling("#text");
                var createDate = DateTime.Parse(dateNode.InnerText.Replace("时间:", "").Trim());

                var authorLiteralNode = dateNode.GetNextSibling("#text");
                if (authorLiteralNode.InnerText.Trim() == "作者:匿名")
                    thread.UserName = "匿名";
                else if (authorLiteralNode.InnerText.Trim() == "作者:")
                {
                    var userNameAnchor = authorLiteralNode.GetNextSibling("a");
                    thread.UserName = userNameAnchor.InnerText.Trim();
                }

                var messageNode = authorLiteralNode.GetNextSibling("div");
                if ("message" != messageNode.GetAttributeValue("class"))
                {
                    messageNode = bodyNode.CssSelect("div.message").First();
                }

                var firstPostHtmlContent = messageNode.InnerHtml;
                var modifyDate = GetModifyDate(messageNode, thread.UserName);
                int pid = messageNode.GetNextSibling2("a").GetAttributeValue("href").GetPostId();

                var post = new Post
                {
                    Id = pid,
                    ThreadId = thread.Id,
                    UserName = thread.UserName,
                    Title = thread.Title,
                    Order = 1,
                    HtmlContent = firstPostHtmlContent,
                    CreateDate = createDate,
                    ModifyDate = modifyDate
                };
                //TODO:set rating 
                thread.Posts.Add(post);
            }

            //Get replies:
            var infobarNodes = bodyNode.CssSelect("div.infobar").ToArray();
            var messageNodes = isFirstPage
                ? bodyNode.CssSelect("div.message").Skip(1).ToArray()//第一个元素是主题，上面已经查看过了。
                : bodyNode.CssSelect("div.message").ToArray();

            if (infobarNodes.Length != messageNodes.Length)
                throw new ProcessFaultException(request, "infobar和message元素个数不一致。");
            if (infobarNodes.Length != 0)
            {
                try
                {
                    var replies = infobarNodes.Select((info, index) =>
                    {
                        var post = new Post
                        {
                            ThreadId = thread.Id,
                            Id = int.Parse(info.GetPreviousSibling("a").GetAttributeValue("name").Replace("pid", ""))
                        };
                        var orderNode = info.CssSelect("b").First();
                        post.Order = int.Parse(orderNode.InnerText.Replace("#", ""));
                        var userNameAnchor = orderNode.GetNextSibling2("a");
                        if (userNameAnchor != null)
                        {
                            post.UserName = userNameAnchor.InnerText;
                        }
                        else
                        {
                            post.UserName = orderNode.NextSibling.InnerText.Trim();
                        }
                        post.CreateDate = DateTime.Parse(info.CssSelect("span.nf").Single().InnerText);
                        var replyBodyNode = messageNodes[index];
                        post.HtmlContent = replyBodyNode.InnerHtml;

                        post.ModifyDate = GetModifyDate(replyBodyNode, post.UserName);
                        //TODO:set rating 
                        return post;
                    });
                    thread.Posts.AddRange(replies);
                }
                catch (Exception e)
                {
                    throw new ProcessFaultException(request, e.Message, e);
                }
            }

            var nextPageUrl = isFirstPage ? null : GetNextThreadPageUrl(request, rootNode);

            return new MillResult<ForumThread>
            {
                Url = request.Url,
                HumanReadableDescription = request.HumanReadableDescription,
                NextPageUrl = nextPageUrl,
                Result = thread
            };
        }

        private int GetThreadPageCurrentIndex(MillRequest request, HtmlNode rootNode)
        {
            if (!request.Url.MatchPageIndex())
                return 1;

            var pagingNode = rootNode.CssSelect("span.paging").FirstOrDefault();
            if (pagingNode == null)
                return 1;

            var currentPageIndexNode = pagingNode.CssSelect("span.s1").Single();
            return int.Parse(currentPageIndexNode.InnerText.Replace("##", ""));
        }

        private DateTime? GetModifyDate(HtmlNode postBodyNode, string userName)
        {
            var modifyDateElement = postBodyNode.CssSelect("i").LastOrDefault();

            if (modifyDateElement == null) return null;

            DateTime? dt = null;
            var re = new Regex(string.Format(@"^\s*本帖最后由 {0} 于 ([-:\d ]+)", Regex.Escape(userName)));
            var match = re.Match(modifyDateElement.InnerText);
            if (match.Success)
            {
                dt = DateTime.Parse(match.Groups[1].Value);
            }
            return dt;
        }

        private void EnsureSignedIn(HtmlNode rootNode, MillRequest request)
        {
            var signedIn = true;
            var footer = rootNode.CssSelect("div#footer").FirstOrDefault();
            if (footer == null)
                signedIn = false;
            else
            {
                var anchorsInFooter = footer.CssSelect("a").ToArray();
                if (anchorsInFooter.Any(link => link.InnerText == "注册")
                    || anchorsInFooter.Any(link => link.InnerText == "登陆"))
                {
                    signedIn = false;
                }
            }

            if (!signedIn)
                throw new NotSignedInException(request);
        }

        private void EnsurePermissionAllowed(HtmlNode bodyNode, MillRequest request)
        {
            if (bodyNode.InnerText.Trim() == "无权查看本主题")
                throw new PermissionDeniedException(request);
        }

        private string GetNextForumPageUrl(HtmlNode rootNode, MillRequest request)
        {
            var currentPageIndexNode = rootNode.CssSelect("span.paging > span.s1").FirstOrDefault();
            if (currentPageIndexNode == null)
                throw new ProcessFaultException(request, "找不到分页元素。");

            var nextPageNode = currentPageIndexNode.GetNextSibling2("a");

            return nextPageNode == null ? null : nextPageNode.GetAttributeValue("href");
        }

        private string GetNextThreadPageUrl(MillRequest request, HtmlNode rootNode)
        {
            var currentPageNode = rootNode.CssSelect("span.paging > span.s1").FirstOrDefault();
            if (currentPageNode == null)
                throw new ProcessFaultException(request, "找不到分页元素。");

            var previous = currentPageNode.GetPreviousSibling("a");
            return previous == null ? null : previous.GetAttributeValue("href");
        }
    }
}