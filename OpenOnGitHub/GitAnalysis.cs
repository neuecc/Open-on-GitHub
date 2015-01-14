using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;

namespace OpenOnGitHub
{
    public enum GitHubUrlType
    {
        Master,
        CurrentBranch,
        CurrentRevision
    }

    public class GitAnalysis : IDisposable
    {
        readonly Repository repository;
        readonly string targetFullPath;

        public bool IsDiscoveredGitRepository => repository != null;

        public GitAnalysis(string targetFullPath)
        {
            this.targetFullPath = targetFullPath;
            var repositoryPath = LibGit2Sharp.Repository.Discover(targetFullPath);
            if (repositoryPath != null)
            {
                this.repository = new LibGit2Sharp.Repository(repositoryPath);
            }
        }

        public string GetGitHubTargetPath(GitHubUrlType urlType)
        {
            switch (urlType)
            {
                case GitHubUrlType.CurrentBranch:
                    return repository.Head.Name.Replace("origin/", "");
                case GitHubUrlType.CurrentRevision:
                    return repository.Commits.First().Id.Sha;
                case GitHubUrlType.Master:
                default:
                    return "master";
            }
        }

        public string BuildGitHubUrl(GitHubUrlType urlType, Tuple<int, int> selectionLineRange)
        {
            // https://github.com/user/repo.git
            var originUrl = repository.Config.Get<string>("remote.origin.url");
            if (originUrl == null) throw new InvalidOperationException("OriginUrl can't found");

            // https://github.com/user/repo
            var urlRoot = (originUrl.Value.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase))
                ? originUrl.Value.Substring(0, originUrl.Value.Length - 4) // remove .git
                : originUrl.Value;

            // foo/bar.cs
            var rootDir = new DirectoryInfo(repository.Info.Path).Parent.FullName;
            var fileIndexPath = targetFullPath.Substring(rootDir.Length + 1).Replace("\\", "/");

            var repositoryTarget = GetGitHubTargetPath(urlType);

            // line selection
            var fragment = (selectionLineRange != null)
                                ? (selectionLineRange.Item1 == selectionLineRange.Item2)
                                    ? string.Format("#L{0}", selectionLineRange.Item1)
                                    : string.Format("#L{0}-{1}", selectionLineRange.Item1, selectionLineRange.Item2)
                                : "";

            var fileUrl = string.Format("{0}/blob/{1}/{2}{3}", urlRoot, repositoryTarget, fileIndexPath, fragment);
            return fileUrl;
        }

        public void Dispose()
        {
            if (repository != null)
            {
                repository.Dispose();
            }
        }
    }
}