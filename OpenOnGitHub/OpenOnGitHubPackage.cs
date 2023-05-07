using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OpenOnGitHub.Providers;
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
    [Guid(PackageGuids.GuidOpenOnGitHubPkgString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class OpenOnGitHubPackage : AsyncPackage
    {
        private static DTE2 _dte;

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
        private SolutionExplorerHelper _solutionExplorer;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switches to the UI thread in order to consume some services used in command initialization
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _dte = (DTE2)GetGlobalService(typeof(DTE));
            _solutionExplorer = new SolutionExplorerHelper((IVsMonitorSelection)await GetServiceAsync(typeof(IVsMonitorSelection)));
            _menuCommandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));

            Assumes.NotNull(_dte);
            Assumes.NotNull(_menuCommandService);

            foreach (var commandContextGuid in PackageGuids.EnumerateCmdSets())
            {
                foreach (var commandId in PackageCommandIDs.Enumerate())
                {
                    var menuCommandId = new CommandID(commandContextGuid, commandId);
                    var menuCommand = new OleMenuCommand(ExecuteCommand, null, CheckCommandAvailability, menuCommandId);
                    _menuCommandService.AddCommand(menuCommand);
                }
            }
        }

        private void CheckCommandAvailability(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var context = GetCommandContext(command);
                var activeFilePath = GetActiveFilePath(context);

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

                var context = GetCommandContext(command);
                var urlType = ToGitHubUrlType(command.CommandID.ID);
                var activeFilePath = GetActiveFilePath(context);
                var textSelection = GetTextSelection(context);
                var gitHubUrl = _provider.GetUrl(_git, activeFilePath, urlType, textSelection);

                Process.Start(gitHubUrl)?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
            }
        }

        private static CommandContext GetCommandContext(MenuCommand command)
        {
            return command.CommandID.Guid.ToString() switch
            {
                PackageGuids.GuidDocumentTabOpenOnGitHubCmdSetString => CommandContext.DocumentTab,
                PackageGuids.GuidOpenOnGitHubCmdSetString => CommandContext.DocumentEditor,
                PackageGuids.GuidSolutionExplorerOpenOnGitHubCmdSetString => CommandContext.SolutionExplorer,
                _ => CommandContext.DocumentEditor
            };
        }

        private string GetActiveFilePath(CommandContext context)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string fileName;

            if (context == CommandContext.SolutionExplorer)
            {
                var selectedFiles = _solutionExplorer.GetSelectedFiles();

                if (selectedFiles.Count != 1)
                {
                    return string.Empty;
                }

                fileName = selectedFiles[0];
            }
            else
            {
                fileName = $"{_dte.ActiveDocument.Path}{_dte.ActiveDocument.Name}";
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

        private static SelectedRange GetTextSelection(CommandContext context)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (context != CommandContext.DocumentEditor ||
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
            _git?.Dispose();
            base.Dispose(disposing);
        }
    }
}
