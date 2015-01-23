using System.Collections.Generic;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace taiyuanhitech.TGFCSpiderman.Persistence
{
    public interface IPostRepository
    {
        void SavePosts(IEnumerable<Post> posts);

        Task<List<PostWithThreadTitle>> SearchAsync(SearchDescriptor descriptor, int pageSize, int pageNumber);
    }
}