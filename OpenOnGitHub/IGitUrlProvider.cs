namespace OpenOnGitHub;

public interface IGitUrlProvider
{
    string GetUrl(NewGitRepository repository, string filePath, GitHubUrlType urlType, SelectedRange selectedRange);
    string GetSelection(SelectedRange selectedRange);
    bool IsUrlTypeAvailable(GitHubUrlType urlType);
}