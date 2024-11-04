using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Web;

namespace OpenOnGitHub.Providers;

// * dev.azure.com
// * visualstudio.com
// * or a private server url such like https://tfs.contoso.com:8080/tfs/Project.
internal sealed class AzureDevOpsUrlProvider : IGitUrlProvider
{
    public string GetUrl(NewGitRepository repository, string filePath, GitHubUrlType urlType,
        SelectedRange selectedRange)
    {
        var fileIndexPath = repository.GetFileIndexPath(filePath);
        var repositoryTarget = repository.GetGitHubTargetPath(urlType);

        var uriBuilder = new UriBuilder(repository.UrlRoot);
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        query["path"] = fileIndexPath;
        var branchOrCommit = urlType is GitHubUrlType.CurrentRevision or GitHubUrlType.CurrentRevisionFull
            ? 'C'
            : 'B';

        query["version"] = $"G{branchOrCommit}{repositoryTarget}";

        query.Add(GetSelectionInternal(selectedRange));

        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri.ToString();
    }

    public string GetSelection(SelectedRange selectedRange)
    {
        var selectionQuery = GetSelectionInternal(selectedRange);

        return selectionQuery.ToString();
    }

    private static NameValueCollection GetSelectionInternal(SelectedRange selectedRange)
    {
        if (selectedRange == SelectedRange.Empty)
        {
           return [];
        }

        var query = HttpUtility.ParseQueryString("");

        query["line"] = selectedRange.TopLine.ToString(CultureInfo.InvariantCulture);
        query["lineEnd"] = selectedRange.BottomLine.ToString(CultureInfo.InvariantCulture);
        query["lineStartColumn"] = selectedRange.TopColumn.ToString(CultureInfo.InvariantCulture);
        query["lineEndColumn"] = selectedRange.BottomColumn.ToString(CultureInfo.InvariantCulture);

        return query;
    }

    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => urlType != GitHubUrlType.CurrentRevision;
}