using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tatti3;
using Tatti3.GameData;

namespace Tests
{
    [TestClass]
    public class StringTableJsonTests
    {
        [TestMethod]
        public void JsonEscape()
        {
            var input = @"
[
  {
    ""id"": ""STRING"",
    ""Key"": ""STRING"",
    ""Value"": ""Terran Marine\\u0000*\\u0000Ground Units""
  },
  {
    ""id"": ""STRING-1"",
    ""Key"": ""STRING-1"",
    ""Value"": ""Thing2\\u001b2\\u001C3""
  },
  {
    ""id"": ""STRING-2"",
    ""Key"": ""STRING-2"",
    ""Value"": ""Bad escape 1\\u00""
  },
  {
    ""id"": ""STRING-3"",
    ""Key"": ""STRING-3"",
    ""Value"": ""Bad escape 2\\u000""
  },
  {
    ""id"": ""STRING-4"",
    ""Key"": ""STRING-4"",
    ""Value"": ""Bad escape 3\\u0""
  },
  {
    ""id"": ""STRING-5"",
    ""Key"": ""STRING-5"",
    ""Value"": ""Ok escape\\u0000""
  },
  {
    ""id"": ""STRING-6"",
    ""Key"": ""STRING-6"",
    ""Value"": ""\\u000g\\u8000\\u0004\\u0005""
  }
]";
            var table = StringTable.FromJson(new MemoryStream(Encoding.UTF8.GetBytes(input)));
            Assert.AreEqual("(None)", table.GetByIndex(0));
            Assert.AreEqual("Terran Marine", table.GetByIndex(1));
            Assert.AreEqual("Thing2\u001b2\u001c3", table.GetByIndex(2));
            Assert.AreEqual("Bad escape 1\\u00", table.GetByIndex(3));
            Assert.AreEqual("Bad escape 2\\u000", table.GetByIndex(4));
            Assert.AreEqual("Bad escape 3\\u0", table.GetByIndex(5));
            Assert.AreEqual("Ok escape", table.GetByIndex(6));
            Assert.AreEqual("\\u000g\\u8000\x04\x05", table.GetByIndex(7));
        }
    }
}
