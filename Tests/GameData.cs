using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tatti3;
using Tatti3.GameData;

namespace Tests
{
    [TestClass]
    public class GameDataTests
    {
        [TestMethod]
        public void LoadDefaultData()
        {
            var fsys = new EmptyFilesystem();
            var gameData = GameData.Open(fsys);
        }
    }

    public class EmptyFilesystem : IFilesystem
    {
        public EmptyFilesystem()
        {
        }

        public Stream OpenFile(string path)
        {
            throw new FileNotFoundException();
        }

        public bool DirectoryExists(string path)
        {
            return false;
        }

        public IEnumerable<string> EnumerateFiles(string dir, string filter)
        {
            return Array.Empty<string>();
        }
    }
}
