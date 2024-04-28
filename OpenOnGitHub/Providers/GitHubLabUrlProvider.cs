using System.Globalization;

namespace OpenOnGitHub.Providers;

internal sealed class GitHubLabUrlProvider : IGitUrlProvider
{
    public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType,
        SelectedRange selectedRange)
    {
        var fileIndexPath = repository.GetFileIndexPath(filePath);
        var repositoryTarget = repository.GetGitHubTargetPath(urlType);

        var uri = $"{repository.UrlRoot}/blob/{repositoryTarget}/{fileIndexPath}";

        uri += GetSelection(selectedRange);

        return uri;
    }

    public string GetSelection(SelectedRange selectedRange)
    {
        var selection = string.Empty;

        if (selectedRange == SelectedRange.Empty)
        {
            return selection;
        }

        selection += "#L" + selectedRange.TopLine.ToString(CultureInfo.InvariantCulture);

        if (selectedRange.TopLine != selectedRange.BottomLine)
        {
            selection += "-L" + selectedRange.BottomLine.ToString(CultureInfo.InvariantCulture);
        }

        return selection;
    }
    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => true;
}