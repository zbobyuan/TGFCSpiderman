using System.Collections.Generic;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public interface IPostRepository
    {
        void SavePosts(IEnumerable<Post> posts);
    }
}