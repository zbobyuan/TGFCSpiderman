using System.Collections.Generic;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class ForumThread : ThreadHeader
    {
        public ForumThread()
        {
            Posts = new List<Post>();
        }

        public virtual List<Post> Posts { get; set; }

        public int CurrentPageIndex { get; set; }
    }
}