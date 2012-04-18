using System.Collections.Generic;

namespace Sep.Git.Tfs.Core
{
    public interface ITfsChangeset
    {
        TfsChangesetInfo Summary { get; }
        LogEntry Apply(IChangesetConverter converter);
        LogEntry CopyTree(IChangesetConverter converter);
        IEnumerable<TfsTreeEntry> GetTree();
    }
}
