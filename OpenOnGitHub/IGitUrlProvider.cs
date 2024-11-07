using System.Threading.Tasks;

namespace OpenOnGitHub;

public interface IGitUrlProvider
{
    Task<string> GetUrlAsync(GitRepository repository, string filePath, GitHubUrlType urlType, SelectedRange selectedRange);
    string GetSelection(SelectedRange selectedRange);
    bool IsUrlTypeAvailable(GitHubUrlType urlType);
}