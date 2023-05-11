namespace OpenOnGitHub;

public interface IGitUrlProvider
{
    string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType, SelectedRange selectedRange);
    bool IsUrlTypeAvailable(GitHubUrlType urlType);
}