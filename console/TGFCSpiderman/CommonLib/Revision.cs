using System;
using SQLite;

namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class Revision
    {
        [PrimaryKey,AutoIncrement]
        public long Id { get; set; }
        public long PostId { get; set; }
        public string Title { get; set; }
        public DateTime CreateDate { get; set; }
        public string HtmlContent { get; set; }
    }
}