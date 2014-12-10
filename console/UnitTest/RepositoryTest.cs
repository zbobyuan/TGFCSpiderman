using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using taiyuanhitech.TGFCSpiderman.CommonLib;
using taiyuanhitech.TGFCSpiderman.Persistence;

namespace UnitTest
{
    [TestClass]
    [DeploymentItem(@"x86\SQLite.Interop.dll", "x86")]
    [DeploymentItem(@"x64\SQLite.Interop.dll", "x64")]
    [DeploymentItem("tgfc.sqlite")]
    [DeploymentItem("System.Data.SQLite.dll")]
    [DeploymentItem("System.Data.SQLite.EF6.dll")]
    [DeploymentItem("System.Data.SQLite.Linq.dll")]
    public class RepositoryTest
    {
        [TestMethod]
        public void SaveAndFetchPostTest()
        {
            var post = new Post
            {
                Id = 1,
                ThreadId = 10,
                Order = 1,
                Title = "my title",
                UserName = "test user",
                CreateDate = DateTime.Parse("2014/10/10"),
                HtmlContent = "post body<br />"
            };
            using (var db = new TgfcDbContext())
            {
                db.Posts.Add(post);
                db.SaveChanges();
            }

            using (var db = new TgfcDbContext())
            {
                Post fetechedPost = db.Posts.First(p => p.Id == post.Id);
                Assert.AreEqual(fetechedPost.Title, post.Title);
            }
        }

        [TestMethod]
        public void SaveAndFetchRevisionTest()
        {
            var revision = new Revision
            {
                PostId = 1,
                CreateDate = DateTime.Parse("2014/11/20"),
                Title = "new title",
                HtmlContent = "new content"
            };
            using (var db = new TgfcDbContext())
            {
                Revision r = db.Revisions.Add(revision);
                db.SaveChanges();
            }
            using (var db = new TgfcDbContext())
            {
                Revision fetchedRevision = db.Revisions.First(r => r.PostId == revision.PostId);
                Assert.AreNotEqual(0, fetchedRevision.Id);
                Assert.AreEqual(revision.PostId, fetchedRevision.PostId);
            }
        }
        [TestMethod]
        public void SaveModifiedPostAndCheckRevision()
        {
            var repo = new PostRepository();

            var postCreatedByBob = new Post
            {
                Id = 10,
                ThreadId = 100,
                Order = 1,
                Title = "my title",
                UserName = "test user",
                CreateDate = DateTime.Parse("2014/10/10"),
                HtmlContent = "post body<br />"
            };
            var postEditedByBob = new Post
            {
                Id = postCreatedByBob.Id,
                ThreadId = postCreatedByBob.ThreadId,
                Order = postCreatedByBob.Order,
                Title = "title edited",
                HtmlContent = "body edited.",
                CreateDate = postCreatedByBob.CreateDate,
                ModifyDate = postCreatedByBob.CreateDate.AddHours(2),
            };
            repo.SavePosts(new[] {postCreatedByBob});
            repo.SavePosts(new[] {postEditedByBob});

            using (var db = new TgfcDbContext())
            {
                Post postInPostsTable = db.Posts.First(p => p.Id == postCreatedByBob.Id);
                Assert.AreEqual(postInPostsTable.Title, postEditedByBob.Title);
                Revision revision = db.Revisions.First(r => r.PostId == postCreatedByBob.Id);
                Assert.AreEqual(revision.Title, postCreatedByBob.Title);
                Assert.AreEqual(revision.HtmlContent, postCreatedByBob.HtmlContent);
                Assert.AreEqual(revision.CreateDate, postCreatedByBob.CreateDate);
            }
        }
    }
}