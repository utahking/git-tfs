using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            get { return Path.Combine(Workdir, "fakescript"); }
        }

        public void SetupFake(Action<FakeHistoryBuilder> script)
        {
            using(var writer = File.CreateText(FakeScript))
            {
                script(new FakeHistoryBuilder(writer));
            }
        }

        public class FakeHistoryBuilder
        {
            TextWriter _writer;
            public FakeHistoryBuilder(TextWriter writer)
            {
                _writer = writer;
            }

            public FakeChangesetBuilder Changeset(int changesetId, string message, DateTime checkinDate)
            {
                _writer.WriteLine("[changeset {0} - {1}]\n{2}\n\n", changesetId, checkinDate, message);
                return new FakeChangesetBuilder(_writer);
            }
        }

        public class FakeChangesetBuilder
        {
            TextWriter _writer;
            public FakeChangesetBuilder(TextWriter writer)
            {
                _writer = writer;
            }

            public FakeChangesetBuilder Change(TfsChangeType changeType, TfsItemType itemType, string tfsPath, string contents = null)
            {
                _writer.WriteLine("[{0} {1} {2}]", changeType, itemType, tfsPath);
                if (contents != null)
                {
                    _writer.WriteLine(contents);
                }
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
            p.EnvironmentVariables["Path"] = CurrentBuildPath + ";" + Environment.GetEnvironmentVariable("Path");
            startInfo.FileName = "git";
            startInfo.Arguments = "tfs " + String.Join(" ", args);
            var process = Process.Start(startInfo);
            process.WaitForExit();
        }

        private string CurrentBuildPath { get { 

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
