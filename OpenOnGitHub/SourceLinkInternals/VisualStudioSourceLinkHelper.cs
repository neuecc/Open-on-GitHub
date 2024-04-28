using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Window = EnvDTE.Window;

namespace OpenOnGitHub.SourceLinkInternals
{
    internal sealed class VisualStudioSourceLinkHelper(IVsDebuggerSymbolSettingsManager120A debuggerManager)
    {
        public string GetSourceLinkDocumentUrl(EnvDTE.Document activeDocument)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var activeWindow = activeDocument?.ActiveWindow;

            if (activeWindow == null)
            {
                return null;
            }

            var documentFullName = activeDocument!.FullName;

            if (activeWindow.Caption.EndsWith("[SourceLink]", StringComparison.Ordinal) != true)
            {
                return null;
            }

            var toolTip = GetWindowTabToolTip(activeWindow);

            var toolTipLines = toolTip?.Split([Environment.NewLine], StringSplitOptions.None);

            if (!(toolTipLines?.Length > 1))
            {
                return null;
            }

            var dllFullName = toolTipLines[1];

            var (signature, pdbFileName) = GetDllMetadata(dllFullName);
            var dbgSym = debuggerManager.GetCurrentSymbolSettings();
            var cachePath = dbgSym.CachePath;

            var pdbFilePath = Path.Combine(cachePath, pdbFileName, signature, pdbFileName);

            var sourceLinkUri = DocumentUriProvider.GetDocumentUri(pdbFilePath, documentFullName);// toolTipLines[0]);

            return sourceLinkUri;
        }

        public static (string signature, string pdbFileName) GetDllMetadata(string dllFilePath)
        {
            using var dllStream = File.OpenRead(dllFilePath);
            using var peReader = new PEReader(dllStream, PEStreamOptions.LeaveOpen);
            var debugDirectories = peReader.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView).OrderByDescending(e => e.IsPortableCodeView).ToArray();
            var codeViewData = peReader.ReadCodeViewDebugDirectoryData(debugDirectories.First());

            return ($"{codeViewData.Guid.ToString("N").ToUpperInvariant()}ffffffff", Path.GetFileName(codeViewData.Path));
        }

        private static string GetWindowTabToolTip(Window docWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string toolTipValue = null;

            foreach (var window in Application.Current.Windows)
            {
                var documentGroupControls = WpfHelper.GetObjectsByTypeName(window as DependencyObject,
                    "Microsoft.VisualStudio.PlatformUI.Shell.Controls.DocumentGroupControl");

                foreach (var documentGroupControl in documentGroupControls)
                {
                    if (documentGroupControl == null) continue;

                    var documentTabItems = WpfHelper.GetObjectsByTypeName(documentGroupControl,
                        "Microsoft.VisualStudio.PlatformUI.Shell.Controls.DocumentTabItem");

                    foreach (var documentTabItem in documentTabItems)
                    {
                        if (documentTabItem is not HeaderedContentControl headeredContentControl) continue;
                        var header = headeredContentControl.Header;

                        if (header == null) continue;

                        var headerTitle = header.GetType().GetProperty("Title");

                        if (headerTitle == null) continue;

                        var headerTitleValue = headerTitle.GetValue(header, null);

                        var headerTitleString = headerTitleValue.GetType().GetProperty("Title");

                        if (headerTitleString == null
                            || !string.Equals(headerTitleString.GetValue(headerTitleValue, null).ToString().Trim(),
                                docWindow.Caption.Trim(), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var headerWindowFrame = header.GetType().GetProperty("WindowFrame");

                        if (headerWindowFrame == null) continue;

                        var headerWindowFrameValue = headerWindowFrame.GetValue(header, null);

                        if (headerWindowFrameValue == null) continue;

                        var toolTip = headerWindowFrameValue.GetType().GetProperty("ToolTip");

                        if (toolTip == null) continue;

                        toolTipValue = toolTip.GetValue(headerWindowFrameValue, null) as string;
                    }
                }
            }

            return toolTipValue;
        }
    }
}