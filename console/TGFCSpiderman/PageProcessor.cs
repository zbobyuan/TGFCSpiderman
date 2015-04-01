using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using CsQuery;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman
{
    public class PageProcessor : IPageProcessor
    {
        public MillResult<List<ThreadHeader>> ProcessForumPage(MillRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                throw new ArgumentNullException("request");

            var root = CQ.CreateDocument(request.HtmlContent);
            EnsureSignedIn(root, request);

            var titles = root.Select("span.title");
            var authors = root.Select("span.author");
            if (titles.Length != authors.Length)
                throw new ProcessFaultException(request, "forum page title和author元素个数不一样。");
            if (titles.Length == 0)
                throw new ProcessFaultException(request, "forum page 没有找到title和author元素。");

            var headers = new List<ThreadHeader>(titles.Length);
            for (var i = 0; i < titles.Length; i++)
            {
                var titleNode = titles[i];
                var authorNode = authors[i];
                headers.Add(GetThreadHeader(titleNode, authorNode, request, i));
            }

            return new MillResult<List<ThreadHeader>>
            {
                Url = request.Url,
                NextPageUrl = GetNextForumPageUrl(root, request),
                Result = headers
            };
        }

        public MillResult<ForumThread> ProcessThreadPage(MillRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.HtmlContent))
                throw new ArgumentNullException("request");

            var root = CQ.CreateDocument(request.HtmlContent);
            EnsureSignedIn(root, request);

            var body = root.Select("div.navbar:first ~ div:first").FirstElement();
            if (body == null || body.HasAttributes)
                throw new ProcessFaultException(request, "无法定位包含内容的div元素");
            var bodyCq = body.Cq();
            EnsurePermissionAllowed(bodyCq, request);

            var currentPageIndex = GetThreadPageCurrentIndex(request, root);
            var isFirstPage = currentPageIndex == 1;
            var thread = new ForumThread
            {
                Url = request.Url,
                Id = request.Url.GetThreadId(),
                CurrentPageIndex = currentPageIndex
            };

            if (isFirstPage)
            {
                ProcessFirstPost(request, bodyCq, thread, root);
            }

            var infobarNodes = bodyCq.Find("div.infobar");
            var messageNodes = isFirstPage ? bodyCq.Find("div.message:gt(0)") : bodyCq.Find("div.message");
            if (infobarNodes.Length != messageNodes.Length)
                throw new ProcessFaultException(request, "infobar和message元素个数不一致。");

            ProcessReplies(request, infobarNodes, messageNodes, thread);

            var nextPageUrl = isFirstPage ? null : GetNextThreadPageUrl(request, root);

            return new MillResult<ForumThread>
            {
                Url = request.Url,
                HumanReadableDescription = request.HumanReadableDescription,
                NextPageUrl = nextPageUrl,
                Result = thread
            };
        }

        #region static helper methods
        private static void ProcessFirstPost(MillRequest request, CQ bodyCq, ForumThread thread, CQ root)
        {
            var titleNode = bodyCq.Find("b:first").FirstOrDefault();
            if (titleNode == null)
            {
                throw new ProcessFaultException(request, "无法定位主题的title元素");
            }
            thread.Title = titleNode.Cq().Text();

            var dateNode = titleNode.NextSibling;
            if (dateNode == null)
                throw new ProcessFaultException(request, "无法定位发帖日期元素");
            dateNode = dateNode.NextSibling;
            if (dateNode == null || dateNode.NodeType != NodeType.TEXT_NODE)
                throw new ProcessFaultException(request, "无法定位发帖日期元素");
            var createDate = DateTime.Parse(dateNode.NodeValue.Replace("时间:", "").Trim());

            var authorLiteralNode = dateNode.NextSibling;
            if (authorLiteralNode == null)
                throw new ProcessFaultException(request, "无法定位主题作者元素");
            authorLiteralNode = authorLiteralNode.NextSibling;
            if (authorLiteralNode == null || authorLiteralNode.NodeType != NodeType.TEXT_NODE)
                throw new ProcessFaultException(request, "无法定位主题作者元素");
            var author = authorLiteralNode.NodeValue;

            if (author == "作者:匿名")
                thread.UserName = "匿名";
            else if (author == "作者:")
            {
                var userNameAnchor = authorLiteralNode.NextElementSibling;
                if (userNameAnchor == null || !"A".Equals(userNameAnchor.NodeName.ToUpper(), StringComparison.OrdinalIgnoreCase))
                    throw new ProcessFaultException(request, "无法定位主题作者元素");
                thread.UserName = userNameAnchor.Cq().Text().Trim();
            }

            var messageNode = bodyCq.Find("div.message:first").FirstElement();
            if (messageNode == null)
                throw new ProcessFaultException(request, "无法定位主题内容元素");

            var firstPostHtmlContent = HttpUtility.HtmlDecode(messageNode.InnerHTML);
            var modifyDate = GetModifyDate(messageNode.Cq(), thread.UserName);
            var pidAnchor = messageNode.Cq().NextAll("a").FirstElement();
            if (pidAnchor == null || pidAnchor["href"] == null)
                pidAnchor = root.Select("a:contains('引用')").FirstElement();
            if (pidAnchor == null)
                throw new ProcessFaultException(request, "无法定位主题中包含pid的锚元素，无法获取pid");
            var pid = pidAnchor["href"].GetPostId();
            var ratings = messageNode.GetRatings();
            var post = new Post
            {
                Id = pid,
                ThreadId = thread.Id,
                UserName = thread.UserName,
                Title = thread.Title,
                Order = 1,
                HtmlContent = firstPostHtmlContent,
                CreateDate = createDate,
                ModifyDate = modifyDate,
                PositiveRate = ratings.Item1,
                NegativeRate = ratings.Item2,
            };
            thread.Posts.Add(post);
        }

        private static void ProcessReplies(MillRequest request, CQ infobarNodes, CQ messageNodes, ForumThread thread)
        {
            if (infobarNodes.Length <= 0) return;
            var replies = infobarNodes.Zip(messageNodes, (infobarNode, messageNode) => new { infobarNode, messageNode }).Select((x, index) =>
            {
                var post = new Post
                {
                    ThreadId = thread.Id,
                    HtmlContent = HttpUtility.HtmlDecode(x.messageNode.InnerHTML)
                };
                var orderAnchor = x.infobarNode.Cq().Find("b a").FirstElement();
                if (orderAnchor == null)
                    throw new ProcessFaultException(request, string.Format("第{0}个回复无法定位楼层锚元素。", index));
                post.Id = orderAnchor["href"].GetPostId();
                post.Order = int.Parse(orderAnchor.InnerText.Replace("#", ""));

                var nextTextNode = orderAnchor.ParentNode.NextSibling;
                if (nextTextNode == null || nextTextNode.NodeType != NodeType.TEXT_NODE)
                    throw new ProcessFaultException(request, string.Format("第{0}个回复无法定位作者元素。", index));
                var text = nextTextNode.Cq().Text();
                post.UserName = text.Trim().EndsWith("匿名")
                    ? "匿名"
                    : orderAnchor.ParentNode.NextElementSibling.Cq().Text();
                post.CreateDate = DateTime.Parse(x.infobarNode.Cq().Find("span.nf:first").Text());
                post.ModifyDate = GetModifyDate(x.messageNode.Cq(), post.UserName);
                var ratings = x.messageNode.GetRatings();
                post.PositiveRate = ratings.Item1;
                post.NegativeRate = ratings.Item2;

                return post;
            });
            thread.Posts.AddRange(replies);
        }

        private static int GetThreadPageCurrentIndex(MillRequest request, CQ root)
        {
            if (!request.Url.MatchPageIndex())
                return 1;

            var pagingNode = root.Select("span.paging:first").FirstOrDefault();
            if (pagingNode == null)
                return 1;

            var currentPageIndexNode = pagingNode.Cq().Find("span.s1").Single();
            return int.Parse(currentPageIndexNode.InnerText.Replace("##", ""));
        }

        private static ThreadHeader GetThreadHeader(IDomObject titleNode, IDomObject authorNode, MillRequest request, int i)
        {
            var titleAnchor = titleNode.Cq().Find("a:first").FirstOrDefault();
            if (titleAnchor == null)
                throw new ProcessFaultException(request, string.Format("第{0}个title元素里面没有a元素。", i));

            var url = titleAnchor["href"];
            var titleText = titleAnchor.Cq().Text().Trim();
            var threadId = url.GetThreadId();
            if (0 == threadId)
                throw new ProcessFaultException(request, string.Format("无法从第{0}个thread url中获取thread id。", i));
            var header = new ThreadHeader
            {
                Id = threadId,
                Url = url,
                Title = titleText,
                ReplyCount = -1
            };
            var authorText = authorNode.Cq().Text();
            if (string.IsNullOrWhiteSpace(authorText))
                return header;
            var values = authorText.Replace("[", "").Replace("]", "").Split('/');
            if (values.Length != 4)
                return header;

            header.UserName = values[0];
            int replyCount;
            if (int.TryParse(values[1], out replyCount))
            {
                header.ReplyCount = replyCount;
            }

            return header;
        }

        private static DateTime? GetModifyDate(CQ postBodyNode, string userName)
        {
            var modifyDateElement = postBodyNode.Find("i:last").FirstOrDefault();

            if (modifyDateElement == null) return null;

            DateTime? dt = null;
            var re = new Regex(string.Format(@"^\s*本帖最后由 {0} 于 ([-:\d ]+)", Regex.Escape(userName)));
            var match = re.Match(modifyDateElement.Cq().Text());
            if (match.Success)
            {
                dt = DateTime.Parse(match.Groups[1].Value);
            }
            return dt;
        }

        private static void EnsureSignedIn(CQ root, MillRequest request)
        {
            var signedIn = true;
            var footer = root.Select("div#footer").FirstOrDefault();
            if (footer == null)
                signedIn = false;
            else
            {
                var anchorsInFooter = footer.Cq().Find("a");
                if (anchorsInFooter.Any(link => link.Cq().Text() == "注册")
                    || anchorsInFooter.Any(link => link.Cq().Text() == "登陆"))
                {
                    signedIn = false;
                }
            }

            if (!signedIn)
                throw new NotSignedInException(request);
        }

        private static void EnsurePermissionAllowed(CQ body, MillRequest request)
        {
            if (body.Text() == "无权查看本主题")
                throw new PermissionDeniedException(request);
        }

        private static string GetNextForumPageUrl(CQ root, MillRequest request)
        {
            var currentPageIndexNode = root.Select("span.paging > span.s1").FirstOrDefault();
            if (currentPageIndexNode == null)
                throw new ProcessFaultException(request, "找不到分页元素。");

            var nextPageNode = currentPageIndexNode.Cq().Next("a").FirstOrDefault();
            return nextPageNode == null ? null : nextPageNode["href"];
        }

        private static string GetNextThreadPageUrl(MillRequest request, CQ root)
        {
            var currentPageNode = root.Select("span.paging > span.s1").FirstElement();
            if (currentPageNode == null)
                throw new ProcessFaultException(request, "找不到分页元素。");

            var previous = currentPageNode.Cq().Prev("a").FirstElement();
            return previous == null ? null : previous["href"];
        }
        #endregion
    }

    internal static class ProcessorExt
    {
        private static readonly Regex ThreadIdRegex = new Regex(@"tid=(\d+)");
        private static readonly Regex PostIdRegex = new Regex(@"pid=(\d+)");
        private static readonly Regex PageIndexRegex = new Regex(@"page=(\d+)");
        private static readonly Regex RatingPeqNRegex = new Regex(@"\+(\d+)/-\d+=\d+");
        private static readonly Regex RatingPositiveRegex = new Regex(@"\+(\d+)/");
        private static readonly Regex RatingNegativeRegex = new Regex(@"^/-(\d+)=");

        public static int GetThreadId(this string url)
        {
            var match = ThreadIdRegex.Match(url);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static int GetPostId(this string url)
        {
            var match = PostIdRegex.Match(url);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static bool MatchPageIndex(this string url)
        {
            return PageIndexRegex.Match(url).Success;
        }

        public static string GetPageIndex(this string url)
        {
            var matchs = PageIndexRegex.Match(url);
            return matchs.Success ? matchs.Groups[1].Value : "1";
        }

        public static Tuple<int, int> GetRatings(this IDomObject messageNode)
        {
            int positiveRate = 0, negativeRate = 0;
            var nextTextNodeOfMessageNode = messageNode.NextSibling;
            if (nextTextNodeOfMessageNode == null)
                return new Tuple<int, int>(positiveRate, negativeRate);
            nextTextNodeOfMessageNode = nextTextNodeOfMessageNode.NextSibling;
            if (nextTextNodeOfMessageNode != null && nextTextNodeOfMessageNode.NodeType == NodeType.TEXT_NODE
                && nextTextNodeOfMessageNode.NodeValue.StartsWith("评分记录("))
            {
                var text = nextTextNodeOfMessageNode.Cq().Text();
                var match = RatingPeqNRegex.Match(text);
                if (match.Success)
                {//正负相等
                    positiveRate = negativeRate = int.Parse(match.Groups[1].Value);
                }
                else
                {
                    if (text.EndsWith("+"))
                    {
                        var bNode = nextTextNodeOfMessageNode.NextSibling;
                        positiveRate = int.Parse(bNode.Cq().Text());
                        var nextText = bNode.NextSibling.Cq().Text();
                        negativeRate = int.Parse(RatingNegativeRegex.Match(nextText).Groups[1].Value);
                    }
                    else if (text.EndsWith("-"))
                    {
                        var bNode = nextTextNodeOfMessageNode.NextSibling;
                        negativeRate = int.Parse(bNode.Cq().Text());
                        positiveRate = int.Parse(RatingPositiveRegex.Match(text).Groups[1].Value);
                    }
                }
            }

            return new Tuple<int, int>(positiveRate, negativeRate);
        }
    }
}