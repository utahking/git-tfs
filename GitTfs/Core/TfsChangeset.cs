using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sep.Git.Tfs.Core.TfsInterop;

namespace Sep.Git.Tfs.Core
{
    public class TfsChangeset : ITfsChangeset
    {
        private readonly ITfsHelper _tfs;
        private readonly IChangeset _changeset;
        private readonly TextWriter _stdout;
        public TfsChangesetInfo Summary { get; set; }

        public TfsChangeset(ITfsHelper tfs, IChangeset changeset, TextWriter stdout)
        {
            _tfs = tfs;
            _changeset = changeset;
            _stdout = stdout;
        }

        public LogEntry Apply(IChangesetConverter converter)
        {
            foreach (var change in Sort(_changeset.Changes))
            {
                Apply(change, converter);
            }
            return MakeNewLogEntry();
        }

        private void Apply(IChange change, IChangesetConverter converter)
        {
            // If you make updates to a dir in TF, the changeset includes changes for all the children also,
            // and git doesn't really care if you add or delete empty dirs.
            if (change.Item.ItemType == TfsItemType.File)
            {
                var pathInGitRepo = Summary.Remote.GetPathInGitRepo(change.Item.ServerItem);
                if (change.ChangeType.IncludesOneOf(TfsChangeType.Rename))
                {
                    Rename(change, pathInGitRepo, converter);
                }
                else if (change.ChangeType.IncludesOneOf(TfsChangeType.Delete))
                {
                    Delete(pathInGitRepo, converter);
                }
                else
                {
                    Update(change, pathInGitRepo, converter);
                }
            }
        }

        private string GetPathInGitRepo(string tfsPath)
        {
            return Summary.Remote.GetPathInGitRepo(tfsPath);
        }

        private void Rename(IChange change, string pathInGitRepo, IChangesetConverter converter)
        {
            var oldPath = GetPathInGitRepo(GetPathBeforeRename(change.Item));
            if (oldPath != null)
            {
                Delete(oldPath, converter);
            }
            if (!change.ChangeType.IncludesOneOf(TfsChangeType.Delete))
            {
                Update(change, pathInGitRepo, converter);
            }
        }

        private IEnumerable<IChange> Sort(IEnumerable<IChange> changes)
        {
            return changes.OrderBy(change => Rank(change.ChangeType));
        }

        private int Rank(TfsChangeType type)
        {
            if (type.IncludesOneOf(TfsChangeType.Delete))
                return 0;
            if (type.IncludesOneOf(TfsChangeType.Rename))
                return 1;
            return 2;
        }

        private string GetPathBeforeRename(IItem item)
        {
            var previousChangeset = item.ChangesetId - 1;
            var oldItem = item.VersionControlServer.GetItem(item.ItemId, previousChangeset);
            if (null == oldItem)
            {
                var history = item.VersionControlServer.QueryHistory(item.ServerItem, item.ChangesetId, 0,
                                                                     TfsRecursionType.None, null, 1, previousChangeset,
                                                                     1, true, false, false);
                var previousChange = history.FirstOrDefault();
                if (previousChange == null)
                {
                    Trace.WriteLine(string.Format("No history found for item {0} changesetId {1}", item.ServerItem, item.ChangesetId));
                    return null;
                }
                oldItem = previousChange.Changes[0].Item;
            }
            return oldItem.ServerItem;
        }

        private void Update(IChange change, string pathInGitRepo, IChangesetConverter converter)
        {
            if(change.Item.DeletionId == 0)
            {
                using(var stream = change.Item.DownloadFile())
                {
                    converter.Update(pathInGitRepo, stream);
                }
            }
        }

        public IEnumerable<TfsTreeEntry> GetTree()
        {
            //var treeInfo = Summary.Remote.Repository.GetObjects();
            return from item in _changeset.VersionControlServer.GetItems(Summary.Remote.TfsRepositoryPath, _changeset.ChangesetId, TfsRecursionType.Full)
                   where item.ItemType == TfsItemType.File
                   let pathInGitRepo = GetPathInGitRepo(item.ServerItem)
                   //let pathInGitRepo = GetPathInGitRepo(item.ServerItem, treeInfo)
                   where pathInGitRepo != null && !Summary.Remote.ShouldSkip(pathInGitRepo)
                   select new TfsTreeEntry(pathInGitRepo, item);
        }

        public LogEntry CopyTree(IChangesetConverter converter)
        {
            var startTime = DateTime.Now;
            var itemsCopied = 0;
            var maxChangesetId = 0;
            var tfsTreeEntries = GetTree().ToArray();
            if (tfsTreeEntries.Length == 0)
            {
                maxChangesetId = _changeset.ChangesetId;
            }
            else
            {
                foreach (var entry in tfsTreeEntries)
                {
                    Add(entry.Item, entry.FullName, converter);
                    maxChangesetId = Math.Max(maxChangesetId, entry.Item.ChangesetId);

                    itemsCopied++;
                    if (DateTime.Now - startTime > TimeSpan.FromSeconds(30))
                    {
                        _stdout.WriteLine("{0} objects created...", itemsCopied);
                        startTime = DateTime.Now;
                    }
                }
            }
            return MakeNewLogEntry(maxChangesetId == _changeset.ChangesetId ? _changeset : _tfs.GetChangeset(maxChangesetId));
        }

        private void Add(IItem item, string pathInGitRepo, IChangesetConverter converter)
        {
            if (item.DeletionId == 0)
            {
                using (var stream = item.DownloadFile())
                {
                    converter.Update(pathInGitRepo, stream);
                }
            }
        }

        private void Delete(string pathInGitRepo, IChangesetConverter converter)
        {
            converter.Remove(pathInGitRepo);
        }

        private LogEntry MakeNewLogEntry()
        {
            return MakeNewLogEntry(_changeset);
        }

        private LogEntry MakeNewLogEntry(IChangeset changesetToLog)
        {
            var identity = _tfs.GetIdentity(changesetToLog.Committer);

            // committer's & author's name and email MUST NOT be empty as otherwise they would be picked
            // by git from user.name and user.email config settings which is bad thing because commit could
            // be different depending on whose machine it fetched
            var name = "Unknown TFS user";
            var email = "unknown@tfs.local";
            if (identity != null)
            {
                if (!String.IsNullOrWhiteSpace(identity.DisplayName))
                    name = identity.DisplayName;
                
                if (!String.IsNullOrWhiteSpace(identity.MailAddress))
                    email = identity.MailAddress;
                else if (!String.IsNullOrWhiteSpace(changesetToLog.Committer))
                    email = changesetToLog.Committer;
            }

            return new LogEntry
                       {
                           Date = changesetToLog.CreationDate, 
                           Log = changesetToLog.Comment + Environment.NewLine, 
                           ChangesetId = changesetToLog.ChangesetId, 
                           CommitterName = name, 
                           AuthorName = name, 
                           CommitterEmail = email, 
                           AuthorEmail = email
                       };
        }
    }
}
