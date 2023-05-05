using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static OpenOnGitHub.OpenOnGitHubPackage;

namespace OpenOnGitHub.Utils
{
    public static class SolutionExplorer
    {
        public static List<string> GetSelectedFiles()
        {
            var hierarchyPtr = IntPtr.Zero;
            var containerPtr = IntPtr.Zero;
            try
            {
                var files = new List<string>();

                if (MonitorSelection != null &&
                     MonitorSelection.GetCurrentSelection(out hierarchyPtr, out var itemid, out var multiSelect, out containerPtr) == VSConstants.S_OK)
                {
                    if (multiSelect != null)
                    {
                        if (multiSelect.GetSelectionInfo(out var numberOfSelectedItems, out _) == VSConstants.S_OK &&
                            numberOfSelectedItems == 2)
                        {
                            var vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            if (multiSelect.GetSelectedItems(0, numberOfSelectedItems, vsItemSelections) == VSConstants.S_OK)
                            {
                                foreach (var selection in vsItemSelections)
                                {
                                    if (TryGetFile(selection.pHier, selection.itemid, out var file))
                                    {
                                        files.Add(file);
                                    }
                                }
                            }
                        }
                    }
                    else if (itemid != VSConstants.VSCOOKIE_NIL &&
                            hierarchyPtr != IntPtr.Zero &&
                            Marshal.GetObjectForIUnknown(hierarchyPtr) is IVsHierarchy hierarchy &&
                            TryGetFile(hierarchy, itemid, out var file))
                    {
                        files.Add(file);
                    }
                }

                return files;
            }
            finally
            {
                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
                if (containerPtr != IntPtr.Zero)
                {
                    Marshal.Release(containerPtr);
                }
            }
        }

        private static bool TryGetFile(IVsHierarchy hierarchy, uint itemid, out string file)
        {
            if (hierarchy != null &&
                hierarchy.GetCanonicalName(itemid, out file) == VSConstants.S_OK &&
                file != null &&
                File.Exists(file))
            {
                return true;
            }
            file = null;
            return false;
        }
    }
}
