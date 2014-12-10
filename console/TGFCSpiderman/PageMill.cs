using System.Collections.Generic;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.TaskQueue;

namespace taiyuanhitech.TGFCSpiderman
{
    class PageMill : TaskRunner<MillRequest>
    {
        static readonly Regex ThreadIdRegex = new Regex(@"tid=(\d+)");
        static readonly Regex PageIndexRegex = new Regex(@"page=(\d+)");

        private string _waitingFetchUrl;
        protected override void ExecuteTask(List<MillRequest> tasks)
        {
            //string pageType = GetPageTypeFromUrl(task.Url);
            //if (pageType == "forum")
            //{
            //    ProcessForumPage(task);
            //}
            //else if (pageType == "thread")
            //{
            //    ProcessThreadPage(task);
            //}
        }
        #region static helopers
        private static string GetPageTypeFromUrl(string url)
        {//I'm not a big fan of regular expression
            if (url.IndexOf("action=thread", StringComparison.InvariantCultureIgnoreCase) > 0)
            {
                return "thread";
            }
            if (url.IndexOf("action=forum", StringComparison.InvariantCultureIgnoreCase) > 0)
            {
                return "forum";
            }

            throw new NotSupportedException(string.Format("正在尝试解析未知的url的页面内容：{0}", url));
        }

        private static string GetThreadIdFromUrl(string url)
        { //url looks like "index.php?action=thread&tid=6880219&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default"
            //Not a big fan of regular expression, but...
            Match match = ThreadIdRegex.Match(url);
            return match.Groups[1].Value;
        }

        private static int GetPageIndexFromUrl(string url)
        {
            Match match = PageIndexRegex.Match(url);
            return match.Success ? int.Parse(match.Groups[1].Value) : 1;
        }

        private static string ChangePageIndex(string url, int index)
        {
            Match match = PageIndexRegex.Match(url);
            if (match.Success)
            {
                return PageIndexRegex.Replace(url, "page=" + index);
            }
            else
            {
                return url.EndsWith("&") ? url + "page=" + index : url + "&page=" + index;
            }
        }
        #endregion

        private void ProcessForumPage(MillRequest request)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(System.Web.HttpUtility.HtmlDecode(request.HtmlContent));
            HtmlNode rootNode = htmlDoc.DocumentNode;
            var titles = rootNode.CssSelect("span.title").ToArray();
            var authors = rootNode.CssSelect("span.author").ToArray();
            Debug.Assert(titles.Length == authors.Length, "forum页title 与 author 个数不一致。");

            for (int i = titles.Length - 1; i >= 0; i--)
            {
                var titleNode = titles[i];
                var authorNode = authors[i];

                var titleAnchor = titleNode.CssSelect("a").First();
                var url = titleAnchor.GetAttributeValue("href");
                var title = titleAnchor.InnerText.Trim();
                var threadId = GetThreadIdFromUrl(url);

                var authorHtml = authorNode.InnerHtml;
                var values = authorHtml.Split('/');

                int replyCount = int.Parse(values[1]);
                if (replyCount < 1000)
                {
                    var thread = new ThreadHeader()
                    {
                        Id = threadId,
                        Url = url,
                        Title = title,
                        UserName = values[0].Substring(1),
                        ReplyCount = replyCount
                    };
                    if (i == titles.Length - 1)
                    {
                        _waitingFetchUrl = thread.Url;
                    }
                    TaskQueueManager.Inst.PageFetcher.EnqueueTask(thread.Url);
                }
            }
        }

        private void ProcessThreadPage(MillRequest task)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(System.Web.HttpUtility.HtmlDecode(task.HtmlContent));
            HtmlNode rootNode = htmlDoc.DocumentNode;

            ForumThread thread = null;
            //先看看是不是第一页吧
            var pageIndex = GetPageIndexFromUrl(task.Url);
            if (pageIndex == 1)
            {//第一页，找出兰州。
                thread = new ForumThread { Url = task.Url };
                var bodyDiv = rootNode.CssSelect("div.navbar").Single().NextSibling.NextSibling;
                var titleElement = bodyDiv.CssSelect("b").First();
                thread.Title = titleElement.InnerText;
                thread.Id = GetThreadIdFromUrl(thread.Url);
                var createDateElement = titleElement.GetNextSibling("#text");
                DateTime createDate = DateTime.Parse(createDateElement.InnerText.Replace("时间:", "").Trim());
                var userNameAnchor = createDateElement.GetNextSibling("a");
                thread.UserName = userNameAnchor.InnerText;

                var postBodyDiv = userNameAnchor.GetNextSibling("div");
                var postBody = postBodyDiv.InnerHtml;
                var modifyDateElement = postBodyDiv.CssSelect("i").LastOrDefault();
                DateTime modifyTime = createDate;

                if (modifyDateElement != null)
                {
                    var re = new Regex(string.Format(@"^\s*本帖最后由 {0} 于 ([-:\d ]+)", thread.UserName));
                    var match = re.Match(modifyDateElement.InnerText);
                    if (match.Success)
                    {
                        modifyTime = DateTime.Parse(match.Groups[1].Value);
                    }
                }


                var post = new Post
                {
                    ThreadId = thread.Id,
                    UserName = thread.UserName,
                    Order = 1,
                    HtmlContent = postBody,
                    CreateDate = createDate,
                    ModifyDate = modifyTime,
                };
                thread.Posts.Add(post);
            }

            //获取回复列表
            var infoBarElements = rootNode.CssSelect("div.infobar").ToArray();
            var messageElements = rootNode.CssSelect("div.message").ToArray();
            Debug.Assert(infoBarElements.Length == messageElements.Length, "thread页infobar 与 message 个数不一致。");
            var posts = infoBarElements.Select((info, index) =>
            {
                var post = new Post() { ThreadId = GetThreadIdFromUrl(task.Url) };
                var anchors = info.CssSelect("a").ToArray();
                post.Order = int.Parse(anchors.First().InnerText.Replace("#",""));
                post.UserName = anchors.Last().InnerText;
                post.CreateDate = DateTime.Parse(info.CssSelect("span.nf").First().InnerText);
                var postBodyDiv = messageElements[index];
                post.HtmlContent = postBodyDiv.InnerHtml;

                var modifyDateElement = postBodyDiv.CssSelect("i").LastOrDefault();

                if (modifyDateElement != null)
                {
                    var re = new Regex(string.Format(@"^\s*本帖最后由 {0} 于 ([-:\d ]+)", post.UserName));
                    var match = re.Match(modifyDateElement.InnerText);
                    if (match.Success)
                    {
                        post.ModifyDate = DateTime.Parse(match.Groups[1].Value);
                    }
                }

                return post;
            });
        }
    }
}
