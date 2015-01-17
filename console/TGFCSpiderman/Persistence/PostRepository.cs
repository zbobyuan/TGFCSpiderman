using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public class PostRepository : IPostRepository
    {
        public void SavePosts(IEnumerable<Post> posts)
        {
            using (var db = new TgfcDbContext())
            {
                db.Database.Log = s => Debug.WriteLine(s);
                foreach (var post in posts)
                {
                    var oldPost = db.Posts.FirstOrDefault(p => p.Id == post.Id);
                    if (oldPost == null)
                    {
                        post.SaveDate = DateTime.Now;
                        db.Posts.Add(post);
                    }
                    else
                    {//Entity Framework tracks change automaticly
                        oldPost.PositiveRate = post.PositiveRate;
                        oldPost.NegativeRate = post.NegativeRate;
                        oldPost.Order = post.Order;
                        oldPost.UserName = post.UserName;//anonymous to real name

                        if (post.ModifyDate.HasValue)
                        {
                            if (post.ModifyDate != oldPost.ModifyDate)
                            {
                                var revision = new Revision
                                {
                                    PostId = post.Id,
                                    CreateDate = oldPost.ModifyDate ?? oldPost.CreateDate,
                                    Title = oldPost.Title,
                                    HtmlContent = oldPost.HtmlContent
                                };
                                db.Revisions.Add(revision);
                                oldPost.ModifyDate = post.ModifyDate;
                                oldPost.Title = post.Title;
                                oldPost.HtmlContent = post.HtmlContent;
                            }
                        }
                        if (db.Entry(oldPost).State == EntityState.Modified)
                        {
                            oldPost.SaveDate = DateTime.Now;
                        }
                    }
                }
                db.SaveChanges();
            }
        }

        public async Task<List<PostWithThreadTitle>> SearchAsync(string userName, string title, string content, DateTime? beginTime, DateTime? endTime, bool topicOnly, int pageSize = 100, int pageNumber = 1)
        {
            using (var db = new TgfcDbContext())
            {
                db.Database.Log = s => Debug.WriteLine(s);
                /** 因为SQLite Entity Framework Provider 会将string.Contains(string)会映射成SQL中的CHARINDEX（）>=0，
                  * 该方法无法正常运行，搜索出来的很多无关记录，所以只好手动实现like %%。
                  */
                var sql = topicOnly ?  "SELECT *, Title AS ThreadTitle FROM Posts AS post WHERE [Order] = 1 "
                    : "SELECT post.*, thread.Title AS ThreadTitle FROM " +
                          "Posts AS post LEFT OUTER JOIN Posts AS thread " +
                          "ON post.ThreadId = thread.ThreadId AND thread.[Order] = 1 " +
                          "WHERE 1 = 1 ";
                var ps = new List<SQLiteParameter>();

                if (!string.IsNullOrEmpty(userName))
                {
                    sql += "AND post.UserName = @un ";
                    ps.Add(new SQLiteParameter("un", userName));
                }
                if (beginTime != null)
                {
                    sql += "AND post.CreateDate >= @bt ";
                    ps.Add(new SQLiteParameter("bt", beginTime));
                }
                if (endTime != null)
                {
                    sql += "AND post.CreateDate <= @et ";
                    ps.Add(new SQLiteParameter("et", beginTime));
                }
                if (!string.IsNullOrEmpty(title))
                {
                    sql += "AND post.Title LIKE @t ";
                    ps.Add(new SQLiteParameter("t", string.Format("%{0}%", title)));
                }
                if (!string.IsNullOrEmpty(content))
                {
                    sql += "AND post.HtmlContent LIKE @c ";
                    ps.Add(new SQLiteParameter("c", string.Format("%{0}%", content)));
                }
                sql += string.Format("ORDER BY post.CreateDate DESC LIMIT {0} OFFSET {1}", pageSize, (pageNumber - 1) * pageSize);

                var result = await db.Database.SqlQuery<PostWithThreadTitle>(sql, ps.ToArray()).ToListAsync();
                return result;
            }
        }

        #region search by LINQ
        /*
         * if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
                {
                    var query = from p in db.Posts
                        join firstPost in db.Posts.Where(x => x.Order == 1) on p.ThreadId equals firstPost.ThreadId
                        orderby p.CreateDate descending
                        select new {Post = p, ThreadTitle = firstPost.Title};

                    if (!string.IsNullOrEmpty(userName))
                    {
                        query = query.Where(p => p.Post.UserName == userName);
                    }
                    if (beginTime != null)
                    {
                        query = query.Where(p => p.Post.CreateDate >= beginTime);
                    }
                    if (endTime != null)
                    {
                        query = query.Where(p => p.Post.CreateDate <= endTime);
                    }
                    query = query.Skip((pageNumber-1)*pageSize).Take(pageSize);
                    var list = await query.ToListAsync();
                    var result = list.Select(p =>
                    {
                        return new PostWithThreadTitle(p.Post) {ThreadTitle = p.ThreadTitle};
                    });
                }
         */
        #endregion
    }
}