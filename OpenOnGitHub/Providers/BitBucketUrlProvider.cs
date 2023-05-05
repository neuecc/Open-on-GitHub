using System.Globalization;

namespace OpenOnGitHub.Providers;

internal sealed class BitBucketUrlProvider : IGitUrlProvider
{
    public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType,
        SelectedRange selectedRange)
    {
        var fileIndexPath = repository.GetFileIndexPath(filePath);
        var repositoryTarget = repository.GetGitHubTargetPath(urlType);

        var uri = $"{repository.UrlRoot}/src/{repositoryTarget}/{fileIndexPath}";

        if (selectedRange == SelectedRange.Empty)
        {
            return uri;
        }

        uri += "#lines-" + selectedRange.TopLine.ToString(CultureInfo.InvariantCulture);

        if (selectedRange.TopLine != selectedRange.BottomLine)
        {
            uri += ":" + selectedRange.BottomLine.ToString(CultureInfo.InvariantCulture);
        }

        return uri;
    }

    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => true;
}