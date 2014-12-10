namespace taiyuanhitech.TGFCSpiderman.CommonLib
{
    public class MillResult<T> : MillRequest
        where T : class
    {
        public string NextPageUrl { get; set; }
        public T Result { get; set; }
    }
}