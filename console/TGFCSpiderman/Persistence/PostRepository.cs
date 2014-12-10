using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
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
                                    CreateDate = oldPost.ModifyDate.HasValue ? oldPost.ModifyDate.Value : oldPost.CreateDate,
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
    }
}