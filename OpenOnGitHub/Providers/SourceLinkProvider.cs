using System;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using OpenOnGitHub.SourceLinkInternals;
using System.Collections.Generic;

namespace OpenOnGitHub.Providers;

internal sealed class SourceLinkProvider(
    DTE2 dte,
    IVsDebuggerSymbolSettingsManager120A debuggerSymbols,
    Func<Uri, IGitUrlProvider> gitProviderByUrl)
{
    private readonly VisualStudioSourceLinkHelper _sourceLinkHelper = new(debuggerSymbols);
    private readonly Dictionary<string, string> _cachedUrls = new();

    public string GetUrl(SelectedRange selectedRange)
    {
        var url = GetUrl();

        if (selectedRange == SelectedRange.Empty || url == null)
        {
            return url;
        }

        var provider = gitProviderByUrl(new Uri(url));

        var uriBuilder = new UriBuilder(url);

        var selection = provider.GetSelection(selectedRange);

        if (!string.IsNullOrEmpty(uriBuilder.Query))
        {
            uriBuilder.Query += $"&{selection}";
        }
        else
        {
            uriBuilder.Query = selection;
        }

        return uriBuilder.Uri.ToString();
    }

    private string GetUrl()
    {
        var documentFullName = dte.ActiveDocument?.FullName;

        if (string.IsNullOrWhiteSpace(documentFullName))
        {
            return null;
        }

        if (!_cachedUrls.TryGetValue(documentFullName, out var url))
        {
            _cachedUrls[documentFullName] = url = _sourceLinkHelper.GetSourceLinkDocumentUrl(dte.ActiveDocument);
        }

        return url;
    }

    public bool IsSourceLink(Document activeDocument)
    {
        return (
                   _cachedUrls.TryGetValue(activeDocument?.FullName ?? "", out var url) 
                   && !string.IsNullOrWhiteSpace(url)
               )
               || activeDocument?.ActiveWindow?.Caption.EndsWith("[SourceLink]") == true;
    }

    public string GetTargetDescription()
    {
        var url = GetUrl();

        if (url == null)
        {
            return null;
        }

        var hash = Regex.Match(url, "/(?<hash>[a-fA-F0-9]+)/|/?version=GC(?<hash>[a-fA-F0-9]+)", RegexOptions.Compiled).Groups["hash"].Value;
        var hashTruncatedByLength = hash.Length > 8 ? hash.Substring(0, 8) : hash;
        return $"revision: {hashTruncatedByLength}... (Full ID)";
    }
}