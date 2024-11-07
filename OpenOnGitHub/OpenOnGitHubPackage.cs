using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
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
        private DTE2 _dte;

        private static readonly IGitUrlProvider AzureDevOpsUrlProvider = new AzureDevOpsUrlProvider();
        private static readonly IGitUrlProvider GitHubLabUrlProvider = new GitHubLabUrlProvider();
        private static readonly Dictionary<string, IGitUrlProvider> UrlProviders = new()
        {
            { "azure.com", AzureDevOpsUrlProvider },
            { "visualstudio.com", AzureDevOpsUrlProvider },
            { "github.com", GitHubLabUrlProvider },
            { "gitlab.com", GitHubLabUrlProvider },
            { "gitea.io", new GiteaUrlProvider() },
            { "gitee.com", new GiteeUrlProvider() },
            { "bitbucket.org", new BitBucketUrlProvider() }
        };

        private GitRepository _git;
        private IGitUrlProvider _provider;
        private SolutionExplorerHelper _solutionExplorer;
        private SourceLinkProvider _sourceLinkProvider;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Switches to the UI thread in order to consume some services used in command initialization
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _dte = (DTE2)GetGlobalService(typeof(DTE));
            Assumes.NotNull(_dte);
            var symbolManager = (IVsDebuggerSymbolSettingsManager120A)GetGlobalService(typeof(SVsShellDebugger));
            Assumes.NotNull(symbolManager);
            _sourceLinkProvider = new SourceLinkProvider(_dte, symbolManager, GetGitProviderByUrl);
            _solutionExplorer = new SolutionExplorerHelper((IVsMonitorSelection)await GetServiceAsync(typeof(IVsMonitorSelection)));
            var menuCommandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            Assumes.NotNull(menuCommandService);

            foreach (var commandContextGuid in PackageGuids.EnumerateCmdSets())
            {
                foreach (var commandId in PackageCommandIDs.Enumerate())
                {
                    var menuCommandId = new CommandID(commandContextGuid, commandId);
                    var menuCommand = new OleMenuCommand(ExecuteCommand, null, CheckCommandAvailability, menuCommandId);
                    menuCommandService.AddCommand(menuCommand);
                }
            }
        }

        private void CheckCommandAvailability(object sender, EventArgs e)
        {
            var jtf = new JoinableTaskFactory(ThreadHelper.JoinableTaskContext);
            jtf.Run(async () =>
            {
                await CheckCommandAvailabilityAsync(sender, e).ConfigureAwait(false);
            });
        }

        private async Task CheckCommandAvailabilityAsync(object sender, EventArgs e)
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
                    try
                    {
                        await _git.InitializeAsync();
                    }
                    catch
                    {
                        command.Enabled = false;
                        command.Text = "error: git not found";
                        return;
                    }
                }

                _provider = GetGitProvider();

                var type = ToGitHubUrlType(command.CommandID.ID);

                if (_git.IsDiscoveredGitRepository)
                {
                    var target = await _git.GetGitHubTargetPathAsync(type);

                    if (type == GitHubUrlType.CurrentBranch && target == _git.MainBranchName)
                    {
                        command.Visible = false;
                    }
                    else if (type == GitHubUrlType.Develop && !_git.HasDevelopBranch)
                    {
                        command.Visible = false;
                    }
                    else
                    {
                        command.Enabled = _provider.IsUrlTypeAvailable(type);
                        command.Text = await _git.GetGitHubTargetDescriptionAsync(type);
                        command.Visible = true;
                    }
                }
                else
                {
                    command.Visible = type != GitHubUrlType.CurrentBranch;

                    if (!_sourceLinkProvider.IsSourceLink(_dte.ActiveDocument)
                        || type != GitHubUrlType.CurrentRevisionFull
                        || context == CommandContext.SolutionExplorer)
                    {
                        command.Enabled = false;
                        command.Text = _git.GetInitialGitHubTargetDescription(type);
                        return;
                    }

                    var description = _sourceLinkProvider.GetTargetDescription();

                    command.Enabled = description != null;
                    command.Text = description ?? _git.GetInitialGitHubTargetDescription(type);
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
            if (!_git.IsDiscoveredGitRepository)
            {
                return null;
            }

            var repositoryUri = new Uri(_git.UrlRoot);
            return GetGitProviderByUrl(repositoryUri);
        }

        private static IGitUrlProvider GetGitProviderByUrl(Uri repositoryUri)
        {
            var host = repositoryUri.Host;
            var urlDomainParts = host.Split('.');

            var domain = urlDomainParts.Length > 2
                ? urlDomainParts[urlDomainParts.Length - 2] + "." + urlDomainParts[urlDomainParts.Length - 1]
                : host;

            if (UrlProviders.TryGetValue(domain, out var provider))
            {
                return provider;
            }

            // Private server url such like https://tfs.contoso.com:8080/tfs/Project.
            if (repositoryUri.Port == 8080
                && repositoryUri.Segments.Length >= 5
                && string.Equals(repositoryUri.Segments[1], "tfs/", StringComparison.Ordinal))
            {
                return AzureDevOpsUrlProvider;
            }

            // Fallback to Git(Hub|Lab) provider as default
            // https://gitlab.contoso.com
            // https://{Self-Managed}/{org or user}/{repo name}

            return GitHubLabUrlProvider;
        }

        private async void ExecuteCommand(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                var isNotSourceLink = !_sourceLinkProvider.IsSourceLink(_dte.ActiveDocument);

                if (!_git.IsDiscoveredGitRepository && isNotSourceLink)
                {
                    command.Enabled = false;
                    return;
                }

                var context = GetCommandContext(command);
                var urlType = ToGitHubUrlType(command.CommandID.ID);
                var activeFilePath = GetActiveFilePath(context);
                var textSelection = GetTextSelection(context);

                var gitHubUrl = isNotSourceLink 
                    ?  await _provider.GetUrlAsync(_git, activeFilePath, urlType, textSelection)
                    : _sourceLinkProvider.GetUrl(textSelection);

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

        private SelectedRange GetTextSelection(CommandContext context)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (context != CommandContext.DocumentEditor ||
                _dte.ActiveDocument?.Selection is not TextSelection selection)
            {
                return SelectedRange.Empty;
            }

            if (selection.IsEmpty)
            {
                return new SelectedRange
                {
                    TopLine = selection.CurrentLine,
                    BottomLine = selection.CurrentLine,
                    TopColumn = selection.CurrentColumn,
                    BottomColumn = selection.CurrentColumn
                };
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
            PackageCommandIDs.OpenDevelop => GitHubUrlType.Develop,
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