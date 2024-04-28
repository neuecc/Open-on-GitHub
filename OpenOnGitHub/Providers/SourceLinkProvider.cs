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
{
    private readonly VisualStudioSourceLinkHelper _sourceLinkHelper = new(debuggerSymbols);

    public string GetUrl(SelectedRange selectedRange)
    {
        var uri = _sourceLinkHelper.GetSourceLinkDocumentUrl(dte.ActiveDocument);

        if (selectedRange == SelectedRange.Empty)
        {
            return uri;
        }

        var provider = gitProviderByUrl(new Uri(uri??""));

        uri += provider.GetSelection(selectedRange);

        return uri;
    }

    public bool IsNotSourceLink(Document activeDocument)
    {
        var activeWindow = activeDocument?.ActiveWindow;

        return activeWindow?.Caption.EndsWith("[SourceLink]") != true;
    }

    public string GetTargetDescription()
    {
        var url = _sourceLinkHelper.GetSourceLinkDocumentUrl(dte.ActiveDocument);

        if (url == null)
        {
            return  null;
        }

        var hash = Regex.Match(url, "/(?<hash>[a-fA-F0-9]+)/", RegexOptions.Compiled).Groups["hash"].Value;
        var hashTruncatedByLength = hash.Length > 8 ? hash.Substring(0, 8) : hash;
        return $"revision: {hashTruncatedByLength}... (Full ID)";
    }
}