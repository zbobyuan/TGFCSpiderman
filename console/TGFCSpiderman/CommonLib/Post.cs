using System;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class Post
    {
        public long Id { get; set; }
        public int ThreadId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; }
        public string UserName { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
        public DateTime? SaveDate { get; set; }
        public string HtmlContent { get; set; }
        public int PositiveRate { get; set; }
        public int NegativeRate { get; set; }
    }
    public class PostWithThreadTitle : Post
    {
        public string ThreadTitle { get; set; }
    }
}