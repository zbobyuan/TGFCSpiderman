using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
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
            using (var conn = new SQLiteConnection(DbName, SQLiteOpenFlags.ReadWrite, true))
            {
                var inserts = new List<Post>();
                var updates = new List<Post>();
                var revisions = new List<Revision>();
                foreach (var post in posts)
                {
                    var post1 = post;
                    //此处千万不能听信ReSharper的替换成FirstOrDefault(p => p.Id == post.Id)，因为SQLiteConnection不足够聪明，如果不提供Where表达式将会把表中所有数据全取出来。
                    // ReSharper disable once ReplaceWithSingleCallToFirstOrDefault
                    var oldPost = conn.Table<Post>().Where(p => p.Id == post1.Id).FirstOrDefault();
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
        }

        public Task<List<PostWithThreadTitle>> SearchAsync(SearchDescriptor descriptor, int pageSize = 100, int pageNumber = 1)
        {
            /** 因为SQLite Entity Framework Provider 会将string.Contains(string)会映射成SQL中的CHARINDEX（）>=0，
              * 该方法无法正常运行，搜索出来的很多无关记录，所以只好手动实现like %%。
            */
            var ps = new List<object>();
            var sql = string.Format("{0}{1}{2}{3}", GetSelectStatement(descriptor),
                GetFromStatement(descriptor),
                GetWhereStatement(descriptor, ps),
                GetOrderBy(descriptor));

            sql += string.Format("LIMIT {0} OFFSET {1}", pageSize, (pageNumber - 1) * pageSize);
            var conn = new SQLiteAsyncConnection(DbName, true);
            var task = conn.QueryAsync<PostWithThreadTitle>(sql, ps.ToArray());
            return task;
        }

        private string GetSelectStatement(SearchDescriptor descriptor)
        {
            return descriptor.TopicOnly ? "SELECT post.*, post.Title AS ThreadTitle " : "SELECT post.*, thread.Title AS ThreadTitle ";
        }
        private string GetFromStatement(SearchDescriptor descriptor)
        {
            if (descriptor.TopicOnly)
            {
                const string sql = "FROM Post AS post INNER JOIN (SELECT ThreadId, {0} FROM Post GROUP BY ThreadId) AS thread ON post.ThreadId = thread.ThreadId ";
                var groupColumns = new List<string>();
                if (descriptor.ReplyEndDate != null || descriptor.SortOrder == "最后回复时间")
                {
                    groupColumns.Add("MAX(CreateDate) AS LastReplyDate");
                }
                if (descriptor.SortOrder == "回复数")
                {
                    groupColumns.Add("MAX([Order]) AS ReplyCount");
                }
                return groupColumns.Count > 0 ? string.Format(sql, string.Join(",", groupColumns.ToArray())) : "FROM Post AS post ";
            }
            else
            {
                return "FROM Post AS post LEFT OUTER JOIN Post AS thread ON post.ThreadId = thread.ThreadId AND thread.[Order] = 1 ";
            }
        }
        private string GetWhereStatement(SearchDescriptor descriptor, List<object> ps)
        {
            var sql = "";
            if (!string.IsNullOrEmpty(descriptor.UserName))
            {
                sql += "AND post.UserName = ? ";
                ps.Add(descriptor.UserName);
            }
            if (descriptor.StartDate != null)
            {
                sql += "AND post.CreateDate >= ? ";
                ps.Add(descriptor.StartDate);
            }
            if (descriptor.EndDate != null)
            {
                sql += "AND post.CreateDate <= ? ";
                ps.Add(descriptor.EndDate);
            }
            if (descriptor.ReplyEndDate != null)
            {
                sql += "AND thread.LastReplyDate >= ? ";
                ps.Add(descriptor.ReplyEndDate);
            }
            if (!string.IsNullOrEmpty(descriptor.Title))
            {
                sql += "AND post.Title LIKE ? ";
                ps.Add(string.Format("%{0}%", descriptor.Title));
            }
            if (!string.IsNullOrEmpty(descriptor.Content))
            {
                sql += "AND post.HtmlContent LIKE ? ";
                ps.Add(string.Format("%{0}%", descriptor.Content));
            }

            if (sql == "")
                return descriptor.TopicOnly ? "WHERE post.[Order] = 1 " : "";
            else
            {
                return (descriptor.TopicOnly ? "WHERE post.[Order] = 1 " : "WHERE 1 = 1 ") + sql;
            }
        }

        private string GetOrderBy(SearchDescriptor descriptor)
        {
            string sql = "ORDER BY ";
            switch (descriptor.SortOrder)
            {
                case "发表时间":
                    sql += "post.CreateDate ";
                    break;
                case "最后回复时间":
                    sql += "thread.LastReplyDate ";
                    break;
                case "回复数":
                    sql += "thread.ReplyCount ";
                    break;
                case "正分":
                    sql += "post.PositiveRate ";
                    break;
                case "负分":
                    sql += "post.NegativeRate ";
                    break;
                case "总分":
                    sql += "post.PositiveRate - post.NegativeRate ";
                    break;
                case "争议度":
                    sql += "CASE WHEN post.PositiveRate = 0 OR post.NegativeRate = 0 THEN 0 " +
                                       "ELSE (post.PositiveRate + post.NegativeRate) * 1.0 * MIN(post.PositiveRate, post.NegativeRate) / MAX(post.PositiveRate, post.NegativeRate) " +
                                       "END ";
                    break;
                default:
                    sql += "1 ";
                    break;
            }
            return sql + "DESC ";
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