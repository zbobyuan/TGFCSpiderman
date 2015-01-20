using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public class PostRepository : IPostRepository
    {
        const string DbName = "tgfc.sqlite";
        public void SavePosts(IEnumerable<Post> posts)
        {
            var currentDate = DateTime.Now;
            var conn = new SQLiteConnection(DbName, SQLiteOpenFlags.ReadWrite, true);
            var inserts = new List<Post>();
            var updates = new List<Post>();
            var revisions = new List<Revision>();
            foreach (var post in posts)
            {
                var oldPost = conn.Table<Post>().FirstOrDefault(p => p.Id == post.Id);
                if (oldPost == null)
                {
                    post.SaveDate = currentDate;
                    inserts.Add(post);
                }
                else
                {
                    if (post.ModifyDate.HasValue && post.ModifyDate != oldPost.ModifyDate)//变了
                    {
                        var revision = new Revision
                        {
                            PostId = post.Id,
                            CreateDate = oldPost.ModifyDate ?? oldPost.CreateDate,
                            Title = oldPost.Title,
                            HtmlContent = oldPost.HtmlContent
                        };
                        revisions.Add(revision);
                        post.SaveDate = currentDate;
                        updates.Add(post);
                    }
                    else
                    {//可能发生变化的只有少数几个属性
                        if (post.PositiveRate != oldPost.PositiveRate
                            || post.NegativeRate != oldPost.NegativeRate
                            || post.UserName != oldPost.UserName
                            || post.Order != oldPost.Order)
                        {
                            post.SaveDate = currentDate;
                            updates.Add(post);
                        }
                    }
                }
            }
            if (inserts.Count > 0 || revisions.Count > 0 || updates.Count > 0)
            {
                conn.RunInTransaction(() =>
                {
                    inserts.ForEach(p => conn.Insert(p));
                    updates.ForEach(p => conn.Update(p));
                    revisions.ForEach(r => conn.Insert(r));
                });
            }
        }

        public Task<List<PostWithThreadTitle>> SearchAsync(string userName, string title, string content, DateTime? beginTime, DateTime? endTime, bool topicOnly, int pageSize = 100, int pageNumber = 1)
        {
            /** 因为SQLite Entity Framework Provider 会将string.Contains(string)会映射成SQL中的CHARINDEX（）>=0，
              * 该方法无法正常运行，搜索出来的很多无关记录，所以只好手动实现like %%。
            */
            var sql = topicOnly ? "SELECT *, Title AS ThreadTitle FROM Post AS post WHERE [Order] = 1 "
                    : "SELECT post.*, thread.Title AS ThreadTitle FROM " +
                          "Post AS post LEFT OUTER JOIN Post AS thread " +
                          "ON post.ThreadId = thread.ThreadId AND thread.[Order] = 1 " +
                          "WHERE 1 = 1 ";
            var ps = new List<object>();

            if (!string.IsNullOrEmpty(userName))
            {
                sql += "AND post.UserName = ? ";
                ps.Add(userName);
            }
            if (beginTime != null)
            {
                sql += "AND post.CreateDate >= ? ";
                ps.Add(beginTime);
            }
            if (endTime != null)
            {
                sql += "AND post.CreateDate <= ? ";
                ps.Add(endTime);
            }
            if (!string.IsNullOrEmpty(title))
            {
                sql += "AND post.Title LIKE ? ";
                ps.Add(string.Format("%{0}%", title));
            }
            if (!string.IsNullOrEmpty(content))
            {
                sql += "AND post.HtmlContent LIKE ? ";
                ps.Add(string.Format("%{0}%", content));
            }
            sql += string.Format("ORDER BY post.CreateDate DESC LIMIT {0} OFFSET {1}", pageSize, (pageNumber - 1) * pageSize);
            var conn = new SQLiteAsyncConnection(DbName, SQLiteOpenFlags.ReadOnly, true);
            return conn.QueryAsync<PostWithThreadTitle>(sql, ps.ToArray());
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