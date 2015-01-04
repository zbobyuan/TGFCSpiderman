using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
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

            var firstThreadHeader = millResult.Result.First();
            Assert.AreEqual("fffhxy",firstThreadHeader.UserName);
            Assert.AreEqual(5, firstThreadHeader.ReplyCount);
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
        [DeploymentItem("HtmlContents\\PermissionDenied.htm")]
        [ExpectedException(typeof(PermissionDeniedException))]
        public void PermissionDeniedPageThrows()
        {
            var pageProcessor = new PageProcessor();
            pageProcessor.ProcessThreadPage(new MillRequest
            {
                Url = "testme",
                HtmlContent = File.ReadAllText("PermissionDenied.htm")
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
                Url = "index.php?action=thread&tid=7023798&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default",
                HtmlContent = File.ReadAllText("ThreadPageNoReply.htm")
            });
            Assert.IsNull(result.NextPageUrl);
            Assert.IsNotNull(result.Result.Title);
            Assert.AreEqual(7023798, result.Result.Id);
            Assert.AreEqual("bobykid", result.Result.UserName);
            Assert.AreEqual(1, result.Result.Posts.Count);
            Assert.AreEqual("bobykid", result.Result.Posts[0].UserName);
            Assert.AreEqual(1, result.Result.Posts[0].Order);
            Assert.AreEqual(21010841, result.Result.Posts[0].Id);
            Assert.AreEqual(DateTime.Parse("2014-12-15 14:20"), result.Result.Posts[0].CreateDate);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\ThreadPage2nd.htm")]
        public void ThreadPage2ndTest()
        {
            var processor = new PageProcessor();
            MillResult<ForumThread> result = processor.ProcessThreadPage(new MillRequest
            {
                Url = "index.php?action=thread&tid=6834324&page=2&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default",
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

        [TestMethod]
        [DeploymentItem("HtmlContents\\FirstPostNegativeOnly.htm")]
        [DeploymentItem("HtmlContents\\FirstPostPositiveOnly.htm")]
        [DeploymentItem("HtmlContents\\FirstPostPositiveEqualsNegative.htm")]
        [DeploymentItem("HtmlContents\\FirstPostPositiveLessThenNegative.htm")]
        public void RatingTest()
        {
            var processor = new PageProcessor();
            var thread = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("FirstPostPositiveEqualsNegative.htm")
            }).Result;

            Assert.AreEqual(5, thread.Posts[0].PositiveRate);
            Assert.AreEqual(5, thread.Posts[0].NegativeRate);
            Assert.AreEqual(0, thread.Posts[1].PositiveRate);
            Assert.AreEqual(0, thread.Posts[1].NegativeRate);

            thread = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("FirstPostPositiveOnly.htm")
            }).Result;
            Assert.AreEqual(0, thread.Posts[0].NegativeRate);
            Assert.AreEqual(52, thread.Posts[0].PositiveRate);
            Assert.AreEqual(1, thread.Posts[18].PositiveRate);
            Assert.AreEqual(0, thread.Posts[18].NegativeRate);

            thread = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("FirstPostNegativeOnly.htm")
            }).Result;
            Assert.AreEqual(617, thread.Posts[0].NegativeRate);
            Assert.AreEqual(0, thread.Posts[0].PositiveRate);

            thread = processor.ProcessThreadPage(new MillRequest
            {
                Url = "url",
                HtmlContent = File.ReadAllText("FirstPostPositiveLessThenNegative.htm")
            }).Result;
            Assert.AreEqual(800, thread.Posts[0].NegativeRate);
            Assert.AreEqual(6, thread.Posts[0].PositiveRate);
        }

        [TestMethod]
        [DeploymentItem("HtmlContents\\Closed.htm")]
        public void ClosedThreadTest()
        {//因为<div class="message">没有关闭标签，主楼HtmlContent和评分信息不对，期待tg修复。
            var processor = new PageProcessor();
            var thread = processor.ProcessThreadPage(new MillRequest
            {
                Url = "index.php?action=thread&tid=6865388&vt=1&tp=100&pp=100&sc=1&vf=0&sm=0&iam=notop-nolight-noattach&css=default&page=1",
                HtmlContent = File.ReadAllText("Closed.htm")
            }).Result;
            Assert.AreEqual(20, thread.Posts.Count);
            Assert.AreEqual("王小猪", thread.Posts[0].UserName);
        }
    }
}