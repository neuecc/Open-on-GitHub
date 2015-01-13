using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenOnGitHub
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // TODO:versions
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidOpenOnGitHubPkgString)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    public sealed class OpenOnGitHubPackage : Package
    {
        private static DTE2 _dte;

        internal static DTE2 DTE
        {
            get
            {
                if (_dte == null)
                {
                    _dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
                }

                return _dte;
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs != null)
            {
                foreach (var item in new[] { PackageCommanddIDs.OpenMaster, PackageCommanddIDs.OpenBranch, PackageCommanddIDs.OpenRevision })
                {
                    var menuCommandID = new CommandID(PackageGuids.guidOpenOnGitHubCmdSet, (int)item);
                    var menuItem = new OleMenuCommand(ExecuteCommand, menuCommandID);
                    menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
                    mcs.AddCommand(menuItem);
                }
            }
        }

        private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            try
            {
                // TODO:is should avoid create GitAnalysis every call?
                var git = new GitAnalysis(GetActiveFilePath());
                if (!git.IsDiscoveredGitRepository)
                {
                    command.Enabled = false;
                    return;
                }

                var type = ToGitHubUrlType(command.CommandID.ID);
                var targetPath = git.GetGitHubTargetPath(type);
                if (type == GitHubUrlType.CurrentBranch && targetPath == "master")
                {
                    command.Visible = false;
                }
                else
                {
                    command.Text = targetPath;
                    command.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                var exstr = ex.ToString();
                Debug.Write(exstr);
                command.Text = "error:" + ex.GetType().Name;
                command.Enabled = false;
            }
        }

        private void ExecuteCommand(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            try
            {
                var git = new GitAnalysis(GetActiveFilePath());
                if (!git.IsDiscoveredGitRepository)
                {
                    return;
                }
                var type = ToGitHubUrlType(command.CommandID.ID);
                var gitHubUrl = git.BuildGitHubUrl(type);
                System.Diagnostics.Process.Start(gitHubUrl); // open browser
            }
            catch (Exception ex)
            {
                Debug.Write(ex.ToString());
            }
        }

        string GetActiveFilePath()
        {
            var info = new FileInfo(DTE.ActiveDocument.Path + DTE.ActiveDocument.Name);
            return info.FullName;
        }

        static GitHubUrlType ToGitHubUrlType(int commandId)
        {
            if (commandId == PackageCommanddIDs.OpenMaster) return GitHubUrlType.Master;
            if (commandId == PackageCommanddIDs.OpenBranch) return GitHubUrlType.CurrentBranch;
            if (commandId == PackageCommanddIDs.OpenRevision) return GitHubUrlType.CurrentRevision;
            else return GitHubUrlType.Master;
        }
    }
}
