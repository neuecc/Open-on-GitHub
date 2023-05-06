﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OpenOnGitHub.Providers;
using OpenOnGitHub.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;

namespace OpenOnGitHub
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", PackageVersion.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidOpenOnGitHubPkgString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class OpenOnGitHubPackage : AsyncPackage
    {
        private static DTE2 _dte;
        internal static IVsMonitorSelection MonitorSelection { get; private set; }

        private static readonly IGitUrlProvider AzureDevOpsUrlProvider = new AzureDevOpsUrlProvider();
        private static readonly IGitUrlProvider GitHubLabUrlProvider = new GitHubLabUrlProvider();
        private static readonly Dictionary<string, IGitUrlProvider> UrlProviders = new()
        {
            { "tfs", AzureDevOpsUrlProvider },
            { "dev.azure.com", AzureDevOpsUrlProvider },
            { "visualstudio.com", AzureDevOpsUrlProvider },
            { "github.com", GitHubLabUrlProvider },
            { "gitlab.com", GitHubLabUrlProvider },
            { "gitea.io", new GiteaUrlProvider() },
            { "bitbucket.org", new BitBucketUrlProvider() }
        };

        private GitRepository _git;
        private IGitUrlProvider _provider;
        private IMenuCommandService _menuCommandService;
        private readonly List<OleMenuCommand> _commands = new();

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switches to the UI thread in order to consume some services used in command initialization
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _dte = (DTE2)GetGlobalService(typeof(DTE));
            MonitorSelection = (IVsMonitorSelection)await GetServiceAsync(typeof(IVsMonitorSelection));
            _menuCommandService = (IMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));

            foreach (var commandId in PackageCommandIDs.Enumerate())
            {
                var menuCommandId = new CommandID(PackageGuids.guidOpenOnGitHubCmdSet, commandId);
                var menuCommand = new OleMenuCommand(ExecuteCommand, menuCommandId);
                menuCommand.BeforeQueryStatus += CheckCommandAvailability;
                menuCommand.Enabled = false;
                _menuCommandService.AddCommand(menuCommand);
                _commands.Add(menuCommand);
            }
        }

        private void CheckCommandAvailability(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var activeFilePath = GetActiveFilePath();

                if (string.IsNullOrEmpty(activeFilePath))
                {
                    command.Enabled = false;
                    return;
                }

                if (_git?.IsInsideRepositoryFolder(activeFilePath) != true)
                {
                    _git?.Dispose();
                    _git = new GitRepository(activeFilePath);

                    _provider = GetGitProvider();
                }

                if (!_git.IsDiscoveredGitRepository)
                {
                    command.Enabled = false;
                    return;
                }

                var type = ToGitHubUrlType(command.CommandID.ID);
                var target = _git.GetGitHubTargetPath(type);

                if (type == GitHubUrlType.CurrentBranch && target == _git.MainBranchName)
                {
                    command.Visible = false;
                }
                else
                {
                    command.Text = _git.GetGitHubTargetDescription(type);
                    command.Enabled = _provider.IsUrlTypeAvailable(type);
                    command.Visible = true;
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                command.Text = "error:" + ex.GetType().Name;
                command.Enabled = false;
            }
        }

        private IGitUrlProvider GetGitProvider()
        {
            var repositoryUri = new Uri(_git.UrlRoot);
            var host = repositoryUri.Host;
            var urlDomainParts = host.Split('.');

            var domain = urlDomainParts.Length > 2
                ? urlDomainParts[urlDomainParts.Length - 2] + "." + urlDomainParts[urlDomainParts.Length - 1]
                : host;

            if (!UrlProviders.TryGetValue(domain, out var provider) &&
                !UrlProviders.TryGetValue(host, out provider))
            {
                if (repositoryUri.Port == 8080
                    && repositoryUri.Segments.Length >= 5
                    && string.Equals(repositoryUri.Segments[1], "tfs/", StringComparison.Ordinal))
                {
                    provider = UrlProviders["tfs"];
                }
            }

            if (provider == null)
            {
                throw new InvalidOperationException($"Unknown repository provider: {repositoryUri}");
            }

            return provider;
        }

        private void ExecuteCommand(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!_git.IsDiscoveredGitRepository)
                {
                    command.Enabled = false;
                    return;
                }

                var urlType = ToGitHubUrlType(command.CommandID.ID);
                var activeFilePath = GetActiveFilePath();
                var textSelection = GetTextSelection();
                var gitHubUrl = _provider.GetUrl(_git, activeFilePath, urlType, textSelection);

                Process.Start(gitHubUrl)?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
            }
        }

        /// <summary>
        /// Checks if the menu was opened from the document
        /// </summary>
        /// <returns>true when the context menu was opened on the document or document tab,
        /// otherwise the menu was opened from the Solution Explorer</returns>
        private static bool IsDocumentContext()
        {
            MonitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out var pvarValue);

            return pvarValue is IVsWindowFrame frame &&
                   ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Type, out var frameType)) &&
                   (int)frameType == (int)__WindowFrameTypeFlags.WINDOWFRAMETYPE_Document;
        }

        private static string GetActiveFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fileName;

            if (IsDocumentContext())
            {
                fileName = $"{_dte.ActiveDocument.Path}{_dte.ActiveDocument.Name}";
            }
            else
            {
                var selectedFiles = SolutionExplorer.GetSelectedFiles();

                if (selectedFiles.Count != 1)
                {
                    return string.Empty;
                }
                fileName = selectedFiles[0];
            }

            var path = GetExactPathName(fileName);

            return path;
        }

        private static string GetExactPathName(string pathName)
        {
            if (!(File.Exists(pathName) || Directory.Exists(pathName)))
                return pathName;

            var directoryInfo = new DirectoryInfo(pathName);

            if (directoryInfo.Parent == null)
            {
                return directoryInfo.Name.ToUpper(CultureInfo.InvariantCulture);
            }

            var directoryName = GetExactPathName(directoryInfo.Parent.FullName);
            var fileSystemInfos = directoryInfo.Parent.GetFileSystemInfos(directoryInfo.Name);
            var fileSystemInfo = fileSystemInfos[0];
            var fileName = fileSystemInfo.Name;

            var exactPathName = Path.Combine(directoryName, fileName);
            return exactPathName;
        }

        private static SelectedRange GetTextSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!IsDocumentContext() ||
                _dte.ActiveDocument?.Selection is not TextSelection selection ||
                selection.IsEmpty)
            {
                return SelectedRange.Empty;
            }

            return new SelectedRange
            {
                TopLine = selection.TopLine,
                BottomLine = selection.BottomLine,
                TopColumn = selection.TopPoint.DisplayColumn,
                BottomColumn = selection.BottomPoint.DisplayColumn
            };
        }

        private static GitHubUrlType ToGitHubUrlType(int commandId) => commandId switch
        {
            PackageCommandIDs.OpenMain => GitHubUrlType.Main,
            PackageCommandIDs.OpenBranch => GitHubUrlType.CurrentBranch,
            PackageCommandIDs.OpenRevision => GitHubUrlType.CurrentRevision,
            PackageCommandIDs.OpenRevisionFull => GitHubUrlType.CurrentRevisionFull,
            _ => GitHubUrlType.Main
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var command in _commands)
                {
                    command.BeforeQueryStatus -= CheckCommandAvailability;
                    _menuCommandService.RemoveCommand(command);
                }
                _commands.Clear();
            }
            _git?.Dispose();
            base.Dispose(disposing);
        }
    }
}
