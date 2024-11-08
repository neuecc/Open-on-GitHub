using OpenOnGitHub.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitReader;
using GitReader.Collections;
using GitReader.Primitive;

namespace OpenOnGitHub
{
    public sealed class GitRepository(string targetFullPath) : IDisposable
    {
        private PrimitiveRepository _innerRepository;
        private string _rootDirectory;
        public string MainBranchName { get; private set; }
        public string DevelopBranchName { get; private set; } // nullable
        public bool IsDiscoveredGitRepository => _innerRepository != null;
        public bool HasDevelopBranch => DevelopBranchName != null;
        public string UrlRoot { get; private set; }

        public async Task InitializeAsync()
        {
            _innerRepository = await Repository.Factory.OpenPrimitiveAsync(targetFullPath);

            // https://github.com/user/repo.git
            if (!_innerRepository.RemoteUrls.TryGetValue("origin", out var originUrl))
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

            var mainBranches = new[] { "main", "master", "develop" }; // development is Contains
            var branches = await _innerRepository.GetBranchHeadReferencesAsync();
            var branchesNames = branches.Select(x => x.Name).ToArray();

            MainBranchName = SearchMainBranchName(branchesNames);
            DevelopBranchName = branchesNames.FirstOrDefault(b => b.ToLower().Contains("develop"));
            if (MainBranchName == DevelopBranchName)
            {
                DevelopBranchName = null;
            }
        }

        string SearchMainBranchName(string[] branchNames)
        {
            string main = null;
            string master = null;
            string develop = null;
            foreach (var item in branchNames)
            {
                if (item.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    main = item;
                    break;
                }
                if (item.Equals("master", StringComparison.OrdinalIgnoreCase))
                {
                    master = item;
                    break;
                }
                if (item.Equals("develop", StringComparison.OrdinalIgnoreCase)) // develop or development...
                {
                    develop = item;
                }
            }

            if (main != null) return main;
            if (master != null) return master;
            if (develop != null) return develop;

            return "main"; // not found, default label
        }

        public bool IsInsideRepositoryFolder(string filePath)
        {
            return filePath.IsSubPathOf(_rootDirectory);
        }

        public string GetFileIndexPath(string fullFilePath)
        {
            return fullFilePath.Substring(_rootDirectory.Length).Replace('\\', '/');
        }

        public async Task<string> GetGitHubTargetPathAsync(GitHubUrlType urlType)
        {
            if (_innerRepository == null)
            {
                return MainBranchName;
            }

            var head = await _innerRepository.GetCurrentHeadReferenceAsync();

            if (head == null)
            {
                return MainBranchName;
            }

            return urlType switch
            {
                GitHubUrlType.Develop => DevelopBranchName ?? "develop",
                GitHubUrlType.CurrentBranch => head.Value.Name.Replace("origin/", ""),
                GitHubUrlType.CurrentRevision => ToString(head.Value.Target.HashCode, 8),
                GitHubUrlType.CurrentRevisionFull => ToString(head.Value.Target.HashCode, head.Value.Target.HashCode.Length * 2),
                _ => MainBranchName
            };
        }

        public string GetInitialGitHubTargetDescription(GitHubUrlType urlType)
        {
            return urlType switch
            {
                GitHubUrlType.Develop => "develop",
                GitHubUrlType.CurrentBranch => "branch",
                GitHubUrlType.CurrentRevision => "revision",
                GitHubUrlType.CurrentRevisionFull => "revision full",
                _ => "main"
            };
        }

        public async Task<string> GetGitHubTargetDescriptionAsync(GitHubUrlType urlType)
        {
            if (_innerRepository == null)
            {
                return MainBranchName;
            }

            var head = await _innerRepository.GetCurrentHeadReferenceAsync();

            if (head == null)
            {
                return MainBranchName;
            }

            return urlType switch
            {
                GitHubUrlType.Develop => DevelopBranchName,
                GitHubUrlType.CurrentBranch => $"branch: {head.Value.Name.Replace("origin/", "")}",
                GitHubUrlType.CurrentRevision => $"revision: {ToString(head.Value.Target.HashCode, 8)}",
                GitHubUrlType.CurrentRevisionFull => $"revision: {ToString(head.Value.Target.HashCode, 8)}... (Full ID)",
                _ => MainBranchName
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
