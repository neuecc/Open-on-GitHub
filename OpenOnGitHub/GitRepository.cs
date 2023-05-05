using LibGit2Sharp;
using OpenOnGitHub.Extensions;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenOnGitHub
{
    public sealed class GitRepository : IDisposable
    {
        private Repository _innerRepository;
        private string _rootDirectory;
        public string MainBranchName { get; private set; }
        public bool IsDiscoveredGitRepository => _innerRepository != null;
        public string UrlRoot { get; private set; }

        public GitRepository(string targetFullPath)
        {
            var repositoryPath = Repository.Discover(targetFullPath);

            if (repositoryPath != null)
            {
                Initialize(repositoryPath);
                return;
            }
            GC.SuppressFinalize(this);
        }

        private void Initialize(string repositoryPath)
        {
            _innerRepository = new Repository(repositoryPath);

            // https://github.com/user/repo.git
            var originUrl = _innerRepository.Config.Get<string>("remote.origin.url") ??
                            throw new InvalidOperationException("OriginUrl can't found");

            // https://github.com/user/repo
            UrlRoot = originUrl.Value.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase)
                ? originUrl.Value.Substring(0, originUrl.Value.Length - 4) // remove .git
                : originUrl.Value;

            // git@github.com:user/repo -> http://github.com/user/repo
            UrlRoot = Regex.Replace(UrlRoot, "^git@(.+):(.+)/(.+)$",
                match => "http://" + string.Join("/", match.Groups.OfType<Group>().Skip(1).Select(group => group.Value)),
                RegexOptions.IgnoreCase);

            // https://user@github.com/user/repo -> https://github.com/user/repo
            UrlRoot = Regex.Replace(UrlRoot, "(?<=^https?://)([^@/]+)@", "");

            //https://github.com/user/repo/ -> https://github.com/user/repo
            UrlRoot = UrlRoot.TrimEnd('/');

            // foo/bar.cs
            _rootDirectory = _innerRepository.Info.WorkingDirectory;

            MainBranchName = _innerRepository.Branches.Select(x => x.FriendlyName)
                .FirstOrDefault(x => new[] { "main", "master" }.Contains(x.ToLower())) ?? "main";
        }

        public bool IsInsideRepositoryFolder(string filePath)
        {
            return filePath.IsSubPathOf(_rootDirectory);
        }

        public string GetFileIndexPath(string fullFilePath)
        {
            return fullFilePath.Substring(_rootDirectory.Length).Replace('\\', '/');
        }

        public string GetGitHubTargetPath(GitHubUrlType urlType)
        {
            return urlType switch
            {
                GitHubUrlType.CurrentBranch => _innerRepository.Head.FriendlyName.Replace("origin/", ""),
                GitHubUrlType.CurrentRevision => _innerRepository.Commits.First().Id.ToString(8),
                GitHubUrlType.CurrentRevisionFull => _innerRepository.Commits.First().Id.Sha,
                _ => MainBranchName
            };
        }

        public string GetGitHubTargetDescription(GitHubUrlType urlType)
        {
            return urlType switch
            {
                GitHubUrlType.CurrentBranch => $"Open Branch: {_innerRepository.Head.FriendlyName.Replace("origin/", "")}",
                GitHubUrlType.CurrentRevision => $"Open Revision: {_innerRepository.Commits.First().Id.ToString(8)}",
                GitHubUrlType.CurrentRevisionFull => $"Open Revision: {_innerRepository.Commits.First().Id.ToString(8)}... (Full ID)",
                _ => $"Open {MainBranchName}"
            };
        }

        public void Dispose()
        {
            _innerRepository?.Dispose();
            _innerRepository = null;
            GC.SuppressFinalize(this);
        }

        ~GitRepository()
        {
            _innerRepository?.Dispose();
        }
    }
}