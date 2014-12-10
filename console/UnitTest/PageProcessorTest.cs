using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using HtmlAgilityPack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSTestExtensions;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace UnitTest
{
    [TestClass]
    public class PageProcessorTest
    {
        [TestMethod]
        [DeploymentItem("HtmlContents\\ForumPageFirst.htm")]
        [DeploymentItem("HtmlContents\\ForumPageLast.htm")]
        public void GetNextForumPageUrlTest()
        {
            var pageProcessor = new PageProcessor();
            var privateObject = new PrivateObject(pageProcessor);

            var page2Url =
                (string) privateObject.Invoke("GetNextForumPageUrl", CreateHtmlNode("ForumPageFirst.htm"), null);
            Assert.IsNotNull(page2Url);

            object nextPageOfLastPageShouldBeNull = privateObject.Invoke("GetNextForumPageUrl",
                CreateHtmlNode("ForumPageLast.htm"), null);
            Assert.IsNull(nextPageOfLastPageShouldBeNull);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\ForumPageFirst.htm")]
        public void ProcessForumPageTest()
        {
            var pageProcessor = new PageProcessor();
            const string url =
                "index.php?action=forum&fid=59&sid=&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default&verify=&page=1";
            MillResult<List<ThreadHeader>> millResult = pageProcessor.ProcessForumPage(new MillRequest
            {
                Url = url,
                HtmlContent = File.ReadAllText("ForumPageFirst.htm")
            });

            Assert.AreEqual(millResult.Url, url);
            Assert.IsNotNull(millResult.Result);
            Assert.AreEqual(100, millResult.Result.Count);
            Assert.AreEqual(url.Replace("page=1", "page=2"), millResult.NextPageUrl);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\NotSignedIn.htm")]
        [ExpectedException(typeof (NotSignedInException))]
        public void NotSignedInPageThrows()
        {
            var pageProcessor = new PageProcessor();
            pageProcessor.ProcessForumPage(new MillRequest
            {
                Url = "testme",
                HtmlContent = File.ReadAllText("NotSignedIn.htm")
            });
        }
        [TestMethod]
        [DeploymentItem("HtmlContents\\Signedin.htm")]
        public void SignedInPageShouldNotThrow()
        {
            var pageProcessor = new PageProcessor();
            var result = pageProcessor.ProcessThreadPage(new MillRequest
            {
                Url = "testme",
                HtmlContent = File.ReadAllText("Signedin.htm")
            });
            Assert.IsTrue(result.Result.Posts.Count > 0);
        }
        

        [TestMethod]
        [DeploymentItem("HtmlContents\\ForumPageMissingPager.htm")]
        public void ForumPageMissingPagerThrows()
        {
            var pageProcessor = new PageProcessor();
            ExceptionAssert.Throws<ProcessFaultException>(() => pageProcessor.ProcessForumPage(new MillRequest
            {
                Url = "testme",
                HtmlContent = File.ReadAllText("ForumPageMissingPager.htm")
            }), "找不到分页元素。");
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\ThreadPageNoReply.htm")]
        public void ThreadPageNoReplyTest()
        {
            var processor = new PageProcessor();
            MillResult<ForumThread> result = processor.ProcessThreadPage(new MillRequest
            {
                Url =
                    "index.php?action=thread&tid=7023798&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default",
                HtmlContent = File.ReadAllText("ThreadPageNoReply.htm")
            });
            Assert.IsNull(result.NextPageUrl);
            Assert.IsNotNull(result.Result.Title);
            Assert.AreEqual(7023798, result.Result.Id);
            Assert.AreEqual("bobykid", result.Result.UserName);
            Assert.AreEqual(1, result.Result.Posts.Count);
            Assert.AreEqual("bobykid", result.Result.Posts[0].UserName);
            Assert.AreEqual(1, result.Result.Posts[0].Order);
            Assert.AreEqual(7023798, result.Result.Posts[0].Id);
            Assert.AreEqual(DateTime.Parse("2014-12-15 14:20"), result.Result.Posts[0].CreateDate);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\ThreadPage2nd.htm")]
        public void ThreadPage2ndTest()
        {
            var processor = new PageProcessor();
            MillResult<ForumThread> result = processor.ProcessThreadPage(new MillRequest
            {
                Url =
                    "index.php?action=thread&tid=6834324&page=2&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default",
                HtmlContent = File.ReadAllText("ThreadPage2nd.htm")
            });

            Assert.IsNotNull(result.NextPageUrl);
            Assert.AreEqual(21043731, result.Result.Posts[0].Id);
            Assert.AreEqual(2, result.Result.CurrentPageIndex);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\Anonymous.htm")]
        [DeploymentItem("HtmlContents\\AnonymousInReplies.htm")]
        public void AnonymousUserTest()
        {
            var processor = new PageProcessor();
            MillResult<ForumThread> result = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("Anonymous.htm")
            });
            Assert.AreEqual("匿名", result.Result.UserName);
            Assert.AreEqual("匿名", result.Result.Posts.First().UserName);

            result = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("AnonymousInReplies.htm")
            });
            
            Assert.AreEqual("匿名",result.Result.Posts[47].UserName);
            Assert.AreEqual(48, result.Result.Posts[47].Order);
            Assert.AreEqual("rocky", result.Result.Posts[1].UserName);
            Assert.AreEqual(2, result.Result.Posts[1].Order);
        }

        private HtmlNode CreateHtmlNode(string htmlFileName)
        {
            string text = File.ReadAllText(htmlFileName);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(HttpUtility.HtmlDecode(text));
            return htmlDoc.DocumentNode;
        }
    }
}