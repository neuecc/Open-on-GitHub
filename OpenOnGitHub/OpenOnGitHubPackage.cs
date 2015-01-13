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
            // sometimes, DTE.ActiveDocument.Path is ToLower but GitHub can't open lower path.
            // fix proper-casing | http://stackoverflow.com/questions/325931/getting-actual-file-name-with-proper-casing-on-windows-with-net
            var path = GetExactPathName(DTE.ActiveDocument.Path + DTE.ActiveDocument.Name);
            return path;
        }

        static string GetExactPathName(string pathName)
        {
            if (!(File.Exists(pathName) || Directory.Exists(pathName)))
                return pathName;

            var di = new DirectoryInfo(pathName);

            if (di.Parent != null)
            {
                return Path.Combine(
                    GetExactPathName(di.Parent.FullName),
                    di.Parent.GetFileSystemInfos(di.Name)[0].Name);
            }
            else
            {
                return di.Name.ToUpper();
            }
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
