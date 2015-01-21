using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public interface IPostRepository
    {
        void SavePosts(IEnumerable<Post> posts);

        Task<List<PostWithThreadTitle>> SearchAsync(string userName, string title, string content, DateTime? beginTime,
            DateTime? endTime, bool topicOnly, string sortOrder, DateTime? replyEndDate, int pageSize, int pageNumber);
    }
}