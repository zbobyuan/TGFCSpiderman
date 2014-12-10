using Microsoft.VisualStudio.TestTools.UnitTesting;
using taiyuanhitech.TGFCSpiderman;

namespace UnitTest
{
    [TestClass]
    public class StringExtenssionsTest
    {
        [TestMethod]
        public void ToFixedLengthTest()
        {
            string s1 = "张三", s2 = null, s3 = "张一二三";
            Assert.AreEqual("张三 ", s1.ToFixedLength(3));
            Assert.AreEqual("   ", s2.ToFixedLength(3));
            Assert.AreEqual("张一二", s3.ToFixedLength(3));
        }
    }
}
