using System;
using System.Diagnostics;
using System.IO;
using GitSharp;
using GitSharp.Core;

namespace Sep.Git.Tfs.Core
{
    public class GitIndexInfo : IDisposable
    {
        public static int Do(IGitRepository repository, Action<GitIndexInfo> indexAction)
        {
            using (var indexInfo = new GitIndexInfo(repository, Environment.GetEnvironmentVariable("GIT_INDEX_FILE")))
            {
                indexAction(indexInfo);
                return indexInfo._nr;
            }
        }

        private int _nr = 0;
        private GitIndex _index;
        private GitSharp.Core.Repository _repositoryToDispose;

        private GitIndexInfo(IGitRepository repository, string indexFile)
        {
            InitializeIndex(repository, indexFile);
        }

        private void InitializeIndex(IGitRepository repository, string indexFile)
        {
            if(String.IsNullOrEmpty(indexFile))
            {
                _index = ((GitSharp.Core.Repository) repository.Repository).Index;
            }
            else
            {
                var coreRepository = (GitSharp.Core.Repository) repository.Repository;
                var repositoryWithTemporaryIndex = new GitSharp.Core.Repository(coreRepository.Directory,
                                                                                coreRepository.WorkingDirectory,
                                                                                null, null,
                                                                                new FileInfo(indexFile));
                _repositoryToDispose = repositoryWithTemporaryIndex;
                _index = repositoryWithTemporaryIndex.Index;
            }
        }

        public int Remove(string path)
        {
            Trace.WriteLine("   D " + path);
            _index.RereadIfNecessary();
            _index.Remove(path);
            _index.write();
            return ++_nr;
        }

        public int Update(string path, Stream stream)
        {
            Trace.WriteLine("   U " + path);
            _index.RereadIfNecessary();
            _index.add(Index.PathEncoding.GetBytes(path), stream.ReadAllBytes());
            _index.write();
            return ++_nr;
        }

        public void Dispose()
        {
            if(_repositoryToDispose != null)
            {
                _repositoryToDispose.Dispose();
                _repositoryToDispose = null;
            }
        }
    }
}
