using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using taiyuanhitech.TGFCSpiderman;
using taiyuanhitech.TGFCSpiderman.CommonLib;

namespace UnitTest
{
    [TestClass]
    public class ThreadHeaderExtenssionsTest
    {
        [TestMethod]
        public void GetThreadPageLastPageIndexTest()
        {
            var specs = new Dictionary<int, int>
            {
                {0,1},
                {1,1},
                {99,1},
                {100,2},
                {101,2},
                {199,2},
                {200,3},
                {888, 9},
                {999,10},
                {1000,11}
            };

            foreach (var s in specs)
            {
                Assert.AreEqual(s.Value, new ThreadHeader{ReplyCount = s.Key}.GetLastPageIndex());
            }
        }

        [TestMethod]
        public void ChangePageIndexTest()
        {
            var url = "index.php?action=forum";
            Assert.AreEqual("index.php?action=forum&page=5", url.ChangePageIndex(5));

            url += "&";
            Assert.AreEqual("index.php?action=forum&page=5", url.ChangePageIndex(5));

            url = "index.php?action=forum&page=21&pp=&css=default";
            Assert.AreEqual(url.Replace("page=21","page=5"), url.ChangePageIndex(5));
        }
    }
}
