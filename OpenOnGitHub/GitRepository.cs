using OpenOnGitHub.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitReader;
using GitReader.Collections;
using GitReader.Structures;

namespace OpenOnGitHub
{
    public sealed class GitRepository : IDisposable
    {
        private StructuredRepository _innerRepository;
        private string _rootDirectory;
        private readonly string _targetFullPath;
        public string MainBranchName { get; private set; }
        public bool IsDiscoveredGitRepository => _innerRepository != null;
        public string UrlRoot { get; private set; }

        public GitRepository(string targetFullPath)
        {
            _targetFullPath = targetFullPath;
        }

        public async Task InitializeAsync()
        {
            _innerRepository = await Repository.Factory.OpenStructureAsync(_targetFullPath);

            // https://github.com/user/repo.git
            if(!_innerRepository.RemoteUrls.TryGetValue("origin", out var originUrl))
            {
                throw new InvalidOperationException("OriginUrl can't found");
            }

            // https://github.com/user/repo
            UrlRoot = originUrl.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase)
                ? originUrl.Substring(0, originUrl.Length - 4) // remove .git
                : originUrl;

            // git@github.com:user/repo -> http://github.com/user/repo
            UrlRoot = Regex.Replace(UrlRoot, "^git@(.+):(.+)/(.+)$",
                match => "http://" + string.Join("/", match.Groups.OfType<Group>().Skip(1).Select(group => group.Value)),
                RegexOptions.IgnoreCase);

            // https://user@github.com/user/repo -> https://github.com/user/repo
            UrlRoot = Regex.Replace(UrlRoot, "(?<=^https?://)([^@/]+)@", "");

            //https://github.com/user/repo/ -> https://github.com/user/repo
            UrlRoot = UrlRoot.TrimEnd('/');

            // foo/bar.cs
            _rootDirectory = Path.GetDirectoryName(_innerRepository.GitPath);

            MainBranchName = _innerRepository.Branches.Values.Select(x => x.Name.ToLower())
                .FirstOrDefault(x => new[] { "main", "master", "develop" }.Any(x.StartsWith)) ?? "main";
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
                GitHubUrlType.CurrentBranch => _innerRepository.Head.Name.Replace("origin/", ""),
                GitHubUrlType.CurrentRevision => ToString(_innerRepository.Head.Head.HashCode, 8),
                GitHubUrlType.CurrentRevisionFull => ToString(_innerRepository.Head.Head.HashCode, _innerRepository.Head.Head.HashCode.Length*2),
                _ => MainBranchName
            };
        }

        public string GetInitialGitHubTargetDescription(GitHubUrlType urlType)
        {
            return urlType switch
            {
                GitHubUrlType.CurrentBranch => "branch",
                GitHubUrlType.CurrentRevision => "revision",
                GitHubUrlType.CurrentRevisionFull => "revision full",
                _ => "main"
            };
        }

        public string GetGitHubTargetDescription(GitHubUrlType urlType)
        {
            return urlType switch
            {
                GitHubUrlType.CurrentBranch => $"branch: {_innerRepository.Head.Name.Replace("origin/", "")}",
                GitHubUrlType.CurrentRevision => $"revision: {ToString(_innerRepository.Head.Head.HashCode, 8)}",
                GitHubUrlType.CurrentRevisionFull => $"revision: {ToString(_innerRepository.Head.Head.HashCode, 8)}... (Full ID)",
                _ => $"{MainBranchName}"
            };
        }

        internal static string ToString(byte[] id, int lengthInNibbles)
        {
            char[] array = new char[lengthInNibbles];
            for (int i = 0; i < (lengthInNibbles & -2); i++)
            {
                int num = i >> 1;
                byte index = (byte)(id[num] >> 4);
                array[i++] = "0123456789abcdef"[index];
                index = (byte)(id[num] & 0xFu);
                array[i] = "0123456789abcdef"[index];
            }

            if ((lengthInNibbles & 1) == 1)
            {
                int num2 = lengthInNibbles >> 1;
                byte index2 = (byte)(id[num2] >> 4);
                array[lengthInNibbles - 1] = "0123456789abcdef"[index2];
            }

            return new string(array);
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
