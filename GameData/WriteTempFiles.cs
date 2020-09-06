using System;
using System.IO;
using System.Collections.Generic;

// Class which tries to prevent writes from corrupting the data by opening
// new files for writing, which are all renamed at once when they have been written.
namespace Tatti3.GameData
{
    class WriteTempFiles : IDisposable
    {
        public WriteTempFiles()
        {
            files = new List<TempFile>();
        }

        public FileStream NewFile(string path)
        {
            var file = new TempFile(path);
            files.Add(file);
            return file.inner;
        }

        public void Commit()
        {
            foreach (var file in files)
            {
                file.Close();
            }
            var moves = new List<TempFile>(files.Count);
            try
            {
                foreach (var file in files)
                {
                    file.Move();
                    moves.Add(file);
                }
            }
            catch (Exception)
            {
                // Moving all files failed, so revert any moves that were done
                foreach (var file in moves)
                {
                    file.RevertMove();
                }
                throw;
            }
            // Delete original files
            foreach (var file in moves)
            {
                file.FinishMove();
            }
        }

        List<TempFile> files;

        void IDisposable.Dispose()
        {
            foreach (var file in files)
            {
                file.DeleteTemp();
            }
        }

        class TempFile
        {
            public TempFile(string path)
            {
                realPath = path;
                tempPath = $"{realPath}.__tmp";
                origNewPath = $"{realPath}.__tmp__2";
                inner = File.Create(tempPath);
                moved = false;
            }

            public void Close()
            {
                inner.Dispose();
            }

            public void Move()
            {
                if (!moved)
                {
                    try
                    {
                        File.Move(realPath, origNewPath, true);
                        moved = true;
                    }
                    catch (Exception) { }
                    File.Move(tempPath, realPath, true);
                }
            }

            public void RevertMove()
            {
                if (moved)
                {
                    File.Move(origNewPath, realPath, true);
                }
            }

            public void FinishMove()
            {
                if (moved)
                {
                    try
                    {
                        File.Delete(origNewPath);
                    }
                    catch (Exception) {}
                }
            }

            bool moved;
            string tempPath;
            string realPath;
            string origNewPath;
            public FileStream inner;

            ~TempFile()
            {
                DeleteTemp();
            }

            public void DeleteTemp()
            {
                if (!moved)
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception) {}
                    moved = true;
                }
            }
        }
    }
}
