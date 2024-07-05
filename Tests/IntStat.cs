using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tatti3;

namespace Tests
{
    [TestClass]
    public class IntStatConversionTests
    {
        [TestMethod]
        public void TestSigned()
        {
            IValueConverter converter = new IntStat.ScaleConverter(1, false, true);
            Func<object, string> conv = (object o) =>
                (string)converter.Convert(o, typeof(string), null, CultureInfo.InvariantCulture);
            Func<object, int> back = (object o) =>
                (int)converter.ConvertBack(o, typeof(int), null, CultureInfo.InvariantCulture);

            Assert.AreEqual("50", conv(50));
            Assert.AreEqual("0", conv(0));
            Assert.AreEqual("-50", conv(-50));

            Assert.AreEqual(-50, back("-50"));
            Assert.AreEqual(0, back("0"));
            Assert.AreEqual(50, back("50"));
        }

        [TestMethod]
        public void TestScale256()
        {
            IValueConverter converter = new IntStat.ScaleConverter(256, false, false);
            Func<object, string> conv = (object o) =>
                (string)converter.Convert(o, typeof(string), null, CultureInfo.InvariantCulture);
            Func<object, uint> back = (object o) =>
                (uint)converter.ConvertBack(o, typeof(uint), null, CultureInfo.InvariantCulture);

            Assert.AreEqual("1", conv(256u));
            Assert.AreEqual("0.5", conv(128u));
            Assert.AreEqual("0.25", conv(64u));
            Assert.AreEqual("0", conv(0u));

            Assert.AreEqual(256u, back("1"));
            Assert.AreEqual(256u, back("1.0"));
            Assert.AreEqual(128u, back("0.5"));
            Assert.AreEqual(64u, back("0.25"));
            Assert.AreEqual(0u, back("0"));
            Assert.AreEqual(0u, back("0.0"));
            Assert.AreEqual(0u, back("-0.0"));
        }

        [TestMethod]
        public void SignedPercent1024()
        {
            IValueConverter converter = new IntStat.ScaleConverter(1024, true, true);
            Func<object, string> conv = (object o) =>
                (string)converter.Convert(o, typeof(string), null, CultureInfo.InvariantCulture);
            Func<object, int> back = (object o) =>
                (int)converter.ConvertBack(o, typeof(int), null, CultureInfo.InvariantCulture);

            Assert.AreEqual("100.0", conv(1024));
            Assert.AreEqual("50.0", conv(512));
            Assert.AreEqual("40.0", conv(410));
            Assert.AreEqual("0.1", conv(1));
            Assert.AreEqual("0.0", conv(0));
            Assert.AreEqual("-100.0", conv(-1024));
            Assert.AreEqual("-50.0", conv(-512));
            Assert.AreEqual("-40.0", conv(-410));

            Assert.AreEqual(1024, back("100"));
            Assert.AreEqual(1024, back("100.0"));
            Assert.AreEqual(1024, back("100.00001"));
            Assert.AreEqual(512, back("50"));
            Assert.AreEqual(410, back("40.0"));
            Assert.AreEqual(410, back("40"));
            Assert.AreEqual(0, back("0"));
            Assert.AreEqual(0, back("0.04"));
            Assert.AreEqual(1, back("0.05"));
            Assert.AreEqual(1, back("0.1"));
            Assert.AreEqual(-1024, back("-100"));
            Assert.AreEqual(-1024, back("-100.0"));
            Assert.AreEqual(-1024, back("-100.00001"));
            Assert.AreEqual(-512, back("-50"));
            Assert.AreEqual(-410, back("-40.0"));
            Assert.AreEqual(-410, back("-40"));
        }
    }
}
