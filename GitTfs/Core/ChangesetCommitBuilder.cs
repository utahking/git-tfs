using System;
using System.Collections.Generic;
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

        public ChangesetCommitBuilder(IGitTfsRemote remote, string initialTreeish, IDictionary<string, GitObject> initialTree, IGitRepository repository)
        {
            _remote = remote;
            _initialTree = initialTree;
            _repository = repository;
        }

        public void Dispose()
        {
        }

        public void Update(string pathInGitRepo, Stream stream)
        {
            var mode = GetMode(pathInGitRepo);
            throw new NotImplementedException();
        }

        public void Remove(string pathInGitRepo)
        {
            throw new NotImplementedException();
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
