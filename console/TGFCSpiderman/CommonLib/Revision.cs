using System;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class Revision
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public string Title { get; set; }
        public DateTime CreateDate { get; set; }
        public string HtmlContent { get; set; }
    }
}