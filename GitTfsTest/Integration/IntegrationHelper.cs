using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sep.Git.Tfs.Core;
using Sep.Git.Tfs.Core.TfsInterop;

namespace Sep.Git.Tfs.Test.Integration
{
    class IntegrationHelper : IDisposable
    {
        #region manage the work directory

        string _workdir;
        private string Workdir
        {
            get
            {
                if(_workdir == null)
                {
                    _workdir = Path.GetTempFileName();
                    File.Delete(_workdir);
                    Directory.CreateDirectory(_workdir);
                }
                return _workdir;
            }
        }

        public void Dispose()
        {
            if (_workdir != null)
            {
                try
                {
                    Directory.Delete(_workdir);
                    _workdir = null;
                }
                catch (Exception e)
                {
                }
            }
        }

        #endregion

        #region set up vsfake script

        public string FakeScript
        {
            get { return Path.Combine(Workdir, "_fakescript"); }
        }

        public void SetupFake(Action<FakeHistoryBuilder> script)
        {
            Directory.CreateDirectory(FakeScript);
            script(new FakeHistoryBuilder(FakeScript));
        }

        public class FakeHistoryBuilder
        {
            string _dir;
            public FakeHistoryBuilder(string dir)
            {
                _dir = dir;
            }

            public FakeChangesetBuilder Changeset(int changesetId, string message, DateTime checkinDate)
            {
                var changesetDir = Path.Combine(_dir, String.Format("{0}-{1}", changesetId, checkinDate.Ticks)).Tap(dir => Directory.CreateDirectory(dir));
                File.WriteAllText(Path.Combine(changesetDir, "checkin_comment.txt"), message);
                return new FakeChangesetBuilder(changesetDir);
            }
        }

        public class FakeChangesetBuilder
        {
            string _dir;
            int _count = 0;

            public FakeChangesetBuilder(string dir)
            {
                _dir = dir;
            }

            public FakeChangesetBuilder Change(TfsChangeType changeType, TfsItemType itemType, string tfsPath)
            {
                return Change(changeType, itemType, tfsPath, _ => {});
            }

            public FakeChangesetBuilder Change(TfsChangeType changeType, TfsItemType itemType, string tfsPath, string contents)
            {
                return Change(changeType, itemType, tfsPath, file => File.WriteAllText(file, contents));
            }

            public FakeChangesetBuilder Change(TfsChangeType changeType, TfsItemType itemType, string tfsPath, Action<string> writeFile)
            {
                _count = _count + 1;
                File.AppendAllText(Path.Combine(_dir, "changes"), String.Format("{0}\t{1}\t{2}\t{3}\n", _count, changeType, itemType, tfsPath));
                writeFile(Path.Combine(_dir, _count.ToString()));
                return this;
            }
        }

        #endregion

        #region run git-tfs

        public string TfsUrl { get { return "http://does/not/matter"; } }

        public void Run(params string[] args)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = Workdir;
            startInfo.EnvironmentVariables["GIT_TFS_CLIENT"] = "Fake";
            startInfo.EnvironmentVariables["GIT_TFS_VSFAKE_SCRIPT"] = FakeScript;
            startInfo.EnvironmentVariables["Path"] = CurrentBuildPath + ";" + Environment.GetEnvironmentVariable("Path");
            startInfo.FileName = @"C:\Program Files\git\cmd\git.cmd";
            startInfo.Arguments = "tfs --debug " + String.Join(" ", args);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            Console.WriteLine("PATH: " + startInfo.EnvironmentVariables["Path"]);
            Console.WriteLine(">> " + startInfo.FileName + " " + startInfo.Arguments);
            var process = Process.Start(startInfo);
            Console.Out.Write(process.StandardOutput.ReadToEnd());
            process.WaitForExit();
        }

        private string CurrentBuildPath
        {
            get
            {
                var path = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                return Path.GetDirectoryName(path);
            }
        }

        #endregion

        #region assertions

        public void AssertGitRepo(string repodir)
        {
            var path = Path.Combine(Workdir, repodir);
            Assert.IsTrue(Directory.Exists(path), path + " should be a directory");
            Assert.IsTrue(Directory.Exists(Path.Combine(path, ".git")), path + " should have a .git dir inside of it");
        }

        public void AssertRef(string repodir, string gitref, string expectedSha)
        {
            Assert.Fail("TODO: AssertRef(" + repodir + ", " + gitref + ", " + expectedSha + ")");
        }

        public void AssertEmptyWorkspace(string repodir)
        {
            var entries = new List<string>(Directory.GetFileSystemEntries(Path.Combine(Workdir, repodir)));
            entries.Remove(".");
            entries.Remove("..");
            entries.Remove(".git");
            Assert.AreEqual("", String.Join(", ", entries.ToArray()), "other entries in " + repodir);
        }

        public void AssertFileInWorkspace(string repodir, string file, string contents)
        {
            var path = Path.Combine(Workdir, repodir, file);
            Assert.AreEqual(contents, File.ReadAllText(path), "Contents of " + path);
        }

        #endregion
    }
}
