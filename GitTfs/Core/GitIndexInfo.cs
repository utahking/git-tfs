using System;
using System.Diagnostics;
using System.IO;
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
        private Repository _repositoryToDispose;

        private GitIndexInfo(IGitRepository repository, string indexFile)
        {
            InitializeIndex(repository, indexFile);
        }

        private void InitializeIndex(IGitRepository repository, string indexFile)
        {
            if(String.IsNullOrEmpty(indexFile))
            {
                _index = ((Repository) repository.Repository).Index;
            }
            else
            {
                var coreRepository = (Repository) repository.Repository;
                var repositoryWithTemporaryIndex = new Repository(coreRepository.Directory,
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
            _Update(path, stream);
            _index.write();
            return ++_nr;
        }

        private void _Update(string path, Stream stream)
        {
            var writer = new ObjectWriter(_index.Repository);
            var newSha1 = writer.WriteBlob(stream.Length, stream);

            var entry = _index.GetEntry(path);
            if (entry != null)
            {
                entry.ForceSet("ObjectId", newSha1);
            }
            else
            {
                _index.addEntry(new FileTreeEntry(null, newSha1, Constants.encode(path), false));
            }
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
