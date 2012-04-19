using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Sep.Git.Tfs.Core
{
    public interface IChangesetConverter : IDisposable
    {
        void Update(string pathInGitRepo, Stream stream);
        void Remove(string pathInGitRepo);
        string WriteTree();
    }

    public class ChangesetCommitBuilder : IChangesetConverter
    {
        readonly IGitTfsRemote _remote;
        IDictionary<string, GitObject> _initialTree;
        IGitRepository _repository;
        string _indexFile;

        public ChangesetCommitBuilder(IGitTfsRemote remote, string initialTreeish, IDictionary<string, GitObject> initialTree, IGitRepository repository, string indexFile)
        {
            _remote = remote;
            _initialTree = initialTree;
            _repository = repository;
            AssertTemporaryIndexClean(initialTreeish);
            _indexFile = indexFile;
        }

        public void Dispose()
        {
        }

        public void Update(string pathInGitRepo, Stream stream)
        {
            var mode = GetMode(pathInGitRepo);
            WithTemporaryIndex(() => GitIndexInfo.Do(_repository, (index) => index.Update(mode, pathInGitRepo, stream)));
        }

        public void Remove(string pathInGitRepo)
        {
            WithTemporaryIndex(() => GitIndexInfo.Do(_repository, (index) => index.Remove(pathInGitRepo)));
        }

        public string WriteTree()
        {
            return _repository.CommandOneline("write-tree");
        }

        private string GetMode(string pathInGitRepo)
        {
            if (_initialTree.ContainsKey(pathInGitRepo) &&
                !String.IsNullOrEmpty(_initialTree[pathInGitRepo].Mode))
            {
                return _initialTree[pathInGitRepo].Mode;
            }
            return Mode.NewFile;
        }

        private void WithTemporaryIndex(Action action)
        {
            Ext.WithTemporaryEnvironment(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_indexFile));
                action();
            }, new Dictionary<string, string> { { "GIT_INDEX_FILE", _indexFile } });
        }

        private void AssertTemporaryIndexClean(string treeish)
        {
            if (string.IsNullOrEmpty(treeish))
            {
                AssertTemporaryIndexEmpty();
                return;
            }
            WithTemporaryIndex(() => AssertIndexClean(treeish));
        }

        private void AssertTemporaryIndexEmpty()
        {
            if (File.Exists(_indexFile))
                File.Delete(_indexFile);
        }

        private static readonly Regex treeShaRegex = new Regex("^tree (" + GitTfsConstants.Sha1 + ")");
        private void AssertIndexClean(string treeish)
        {
            if (!File.Exists(_indexFile)) _repository.CommandNoisy("read-tree", treeish);
            var currentTree = _repository.CommandOneline("write-tree");
            var expectedCommitInfo = _repository.Command("cat-file", "commit", treeish);
            var expectedCommitTree = treeShaRegex.Match(expectedCommitInfo).Groups[1].Value;
            if (expectedCommitTree != currentTree)
            {
                Trace.WriteLine("Index mismatch: " + expectedCommitTree + " != " + currentTree);
                Trace.WriteLine("rereading " + treeish);
                File.Delete(_indexFile);
                _repository.CommandNoisy("read-tree", treeish);
                currentTree = _repository.CommandOneline("write-tree");
                if (expectedCommitTree != currentTree)
                {
                    throw new Exception("Unable to create a clean temporary index: trees (" + treeish + ") " + expectedCommitTree + " != " + currentTree);
                }
            }
        }
    }

    public class CasePreserver : IChangesetConverter
    {
        IChangesetConverter _nested;
        IDictionary<string, GitObject> _initialTree;

        public CasePreserver(IChangesetConverter nested, IDictionary<string, GitObject> initialTree)
        {
            _nested = nested;
            _initialTree = initialTree;
        }

        public void Dispose()
        {
            if (_nested != null) _nested.Dispose();
            _nested = null;
        }

        public void Update(string pathInGitRepo, Stream stream)
        {
            _nested.Update(FixCasing(pathInGitRepo), stream);
        }

        public void Remove(string pathInGitRepo)
        {
            _nested.Remove(FixCasing(pathInGitRepo));
        }

        public string WriteTree()
        {
            return _nested.WriteTree();
        }

        private static readonly Regex SplitDirnameFilename = new Regex("(?<dir>.*)/(?<file>[^/]+)");

        private string FixCasing(string pathInGitRepo)
        {
            if (_initialTree.ContainsKey(pathInGitRepo))
                return _initialTree[pathInGitRepo].Path;

            var fullPath = pathInGitRepo;
            var splitResult = SplitDirnameFilename.Match(pathInGitRepo);
            if (splitResult.Success)
            {

                var dirName = splitResult.Groups["dir"].Value;
                var fileName = splitResult.Groups["file"].Value;
                fullPath = FixCasing(dirName) + "/" + fileName;
            }
            _initialTree[fullPath] = new GitObject {Path = fullPath};
            return fullPath;
        }
    }
}
