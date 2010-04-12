using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using CommandLine.OptParse;
using GitSharp;
using GitSharp.Commands;
using Sep.Git.Tfs.Core;
using StructureMap;

namespace Sep.Git.Tfs.Commands
{
    [Pluggable("init")]
    [Description("init [options] tfs-url repository-path [git-repository]")]
    public class Init : GitTfsCommand
    {
        private readonly InitOptions initOptions;
        private readonly RemoteOptions remoteOptions;
        private readonly Globals globals;
        private readonly IGitHelpers gitHelper;

        public Init(RemoteOptions remoteOptions, InitOptions initOptions, Globals globals, IGitHelpers gitHelper)
        {
            this.remoteOptions = remoteOptions;
            this.gitHelper = gitHelper;
            this.globals = globals;
            this.initOptions = initOptions;
        }

        public IEnumerable<IOptionResults> ExtraOptions
        {
            get
            {
                return this.MakeOptionResults(initOptions, remoteOptions);
            }
        }

        public int Run(IList<string> args)
        {
            if (args.Count < 2 || args.Count > 3)
                return Help.ShowHelpForInvalidArguments(this);

            var tfsUrl = args[0];
            var tfsRepositoryPath = args[1];
            var gitRepositoryPath = args.Count == 3 ? args[2] : ".";
            DoGitInitDb(gitRepositoryPath);
            GitTfsInit(tfsUrl, tfsRepositoryPath);
            globals.Repository.Repository.Config.Persist();
            return GitTfsExitCodes.OK;
        }

        private void DoGitInitDb(string path)
        {
            globals.Repository = gitHelper.MakeRepository(GetRepository(path));
            globals.GitDir = globals.Repository.GitDir;
            SetConfig("core.filemode", "false");
            SetConfig("core.symlinks", "false");
            SetConfig("core.ignorecase", "true");
        }

        private Repository GetRepository(string path)
        {
            try
            {
                return new Repository(path);
            }
            catch (Exception)
            {
                return Repository.Init(BuildInitCommandForGitSharp(path));
            }
        }

        private InitCommand BuildInitCommandForGitSharp(string path)
        {
            var initCommand = new InitCommand();
            initCommand.Quiet = false;
            initCommand.GitDirectory = path;
            if (initOptions.GitInitTemplate != null)
                initCommand.Template = initOptions.GitInitTemplate;
            else if (initOptions.GitInitShared != null)
                initCommand.Shared = initOptions.GitInitShared.ToString();
            return initCommand;
        }

        private void GitTfsInit(string tfsUrl, string tfsRepositoryPath)
        {
            SetConfig("core.autocrlf", "false");
            // TODO - check that there's not already a repository configured with this ID.
            SetTfsConfig("url", tfsUrl);
            SetTfsConfig("repository", tfsRepositoryPath);
            SetTfsConfig("fetch", "refs/remotes/" + globals.RemoteId + "/master");
            if (initOptions.NoMetaData) SetTfsConfig("no-meta-data", 1);
            if (remoteOptions.Username != null) SetTfsConfig("username", remoteOptions.Username);
            if (remoteOptions.IgnoreRegex != null) SetTfsConfig("ignore-paths", remoteOptions.IgnoreRegex);
        }

        private void SetTfsConfig(string subkey, object value)
        {
            SetConfig(globals.RemoteConfigKey(subkey), value);
        }

        private void SetConfig(string configKey, object value)
        {
            globals.Repository.Repository.Config[configKey] = value.ToString();
        }
    }
}
