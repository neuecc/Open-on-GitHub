﻿using OpenOnGitHub.Extensions;
using System.Globalization;
using System.Threading.Tasks;

namespace OpenOnGitHub.Providers;

internal sealed class GiteaUrlProvider : IGitUrlProvider
{
    public async Task<string> GetUrlAsync(GitRepository repository, string filePath, GitHubUrlType urlType,
        SelectedRange selectedRange)
    {
        var fileIndexPath = repository.GetFileIndexPath(filePath);
        var repositoryTarget = await repository.GetGitHubTargetPathAsync(urlType);

        var branchOrCommit = urlType is GitHubUrlType.CurrentRevision or GitHubUrlType.CurrentRevisionFull
            ? "commit"
            : "branch";

        var uri = repository.UrlRoot.AppendUriPathSegments("src", branchOrCommit, repositoryTarget, fileIndexPath);
        var uriWithSelection = uri + GetSelection(selectedRange);

        return uriWithSelection;
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