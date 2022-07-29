using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tatti3.GameData
{
    public interface IFilesystem
    {
        Stream OpenFile(string path);
        bool DirectoryExists(string path);
        IEnumerable<string> EnumerateFiles(string dir, string filter);
    }

    public class OsFilesystem : IFilesystem
    {
        public OsFilesystem(string rootDir)
        {
            if (!Directory.Exists(rootDir))
            {
                throw new DirectoryNotFoundException(rootDir);
            }
            Root = rootDir;
        }

        public Stream OpenFile(string path)
        {
            var filename = Path.Join(Root, path);
            return File.OpenRead(filename);
        }

        public bool DirectoryExists(string path)
        {
            var fullPath = Path.Join(Root, path);
            return Directory.Exists(fullPath);
        }

        public IEnumerable<string> EnumerateFiles(string dir, string filter)
        {
            var fullPath = Path.Join(Root, dir);
            return Directory.EnumerateFiles(fullPath, filter)
                .Select(x => Path.GetRelativePath(Root, x));
        }

        private string Root;
    }
}
