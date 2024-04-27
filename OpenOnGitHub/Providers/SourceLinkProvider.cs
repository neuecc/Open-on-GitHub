using System;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using OpenOnGitHub.SourceLinkInternals;

namespace OpenOnGitHub.Providers;

internal sealed class SourceLinkProvider(
    DTE2 dte,
    IVsDebuggerSymbolSettingsManager120A debuggerSymbols,
    Func<Uri, IGitUrlProvider> gitProviderByUrl)
    : IGitUrlProvider
{
    private readonly VisualStudioIntegration _sourceLinkVsIntegration = new(debuggerSymbols);

    public string GetUrl(GitRepository repository, string filePath, GitHubUrlType urlType, SelectedRange selectedRange)
    {
        var uri = _sourceLinkVsIntegration.GetUrl(dte.ActiveDocument);

        if (selectedRange == SelectedRange.Empty)
        {
            return uri;
        }

        var provider = gitProviderByUrl(new Uri(uri??""));

        uri += provider.GetSelection(selectedRange);

        return uri;
    }

    public string GetSelection(SelectedRange selectedRange)
    {
        return string.Empty;
    }

    public bool IsUrlTypeAvailable(GitHubUrlType urlType) => urlType == GitHubUrlType.CurrentRevisionFull;

    public bool IsNotSourceLink(Document activeDocument)
    {
        var activeWindow = activeDocument?.ActiveWindow;

        return activeWindow?.Caption.EndsWith("[SourceLink]") != true;
    }

    public string GetTargetDescription()
    {
        var url = _sourceLinkVsIntegration.GetUrl(dte.ActiveDocument) ?? "NotFound";
        return Regex.Match(url, @"/(?<hash>[a-fA-F0-9]+)/", RegexOptions.Compiled).Groups["hash"].Value;
    }
}