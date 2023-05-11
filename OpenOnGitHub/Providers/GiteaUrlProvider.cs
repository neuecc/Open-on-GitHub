using System.Globalization;

namespace OpenOnGitHub.Providers;

internal sealed class GiteaUrlProvider : IGitUrlProvider
{
    public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType,
        SelectedRange selectedRange)
    {
        var fileIndexPath = repository.GetFileIndexPath(filePath);
        var repositoryTarget = repository.GetGitHubTargetPath(urlType);

        var branchOrCommit = urlType is GitHubUrlType.CurrentRevision or GitHubUrlType.CurrentRevisionFull
            ? "commit"
            : "branch";

        var uri = $"{repository.UrlRoot}/src/{branchOrCommit}/{repositoryTarget}/{fileIndexPath}";

        if (selectedRange == SelectedRange.Empty)
        {
            return uri;
        }

        uri += "#L" + selectedRange.TopLine.ToString(CultureInfo.InvariantCulture);

        if (selectedRange.TopLine != selectedRange.BottomLine)
        {
            uri += "-" + selectedRange.BottomLine.ToString(CultureInfo.InvariantCulture);
        }

        return uri;
    }

    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => true;
}