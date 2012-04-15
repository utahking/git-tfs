using System;

namespace Sep.Git.Tfs.Core
{
    public class ChangesetCommitBuilder : IDisposable
    {
        readonly IGitTfsRemote _remote;
        readonly LogEntry _logEntry;

        public ChangesetCommitBuilder(GitTfsRemote remote, string lastCommit)
        {
            _remote = remote;
            _logEntry = new LogEntry();
            if (!string.IsNullOrEmpty(lastCommit)) _logEntry.CommitParents.Add(lastCommit);
        }

        public void Dispose()
        {
        }

        public LogEntry GetResult()
        {
            _logEntry.Tree = was("Repository.CommandOneline(\"write-tree\")");
            return _logEntry;
        }
    }
}
