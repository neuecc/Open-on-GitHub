using System;
using System.Collections.Generic;

namespace OpenOnGitHub
{
    static class PackageGuids
    {
        public const string guidOpenOnGitHubPkgString = "465b40b6-311a-4e37-9556-95fced2de9c6";
        public const string guidOpenOnGitHubCmdSetString = "a674aaec-a6f5-4df0-9749-e7bef776df5d";
        public const string guidDocumentTabOpenOnGitHubCmdSetString = "d676cf29-179a-4595-aeba-d6fe98a0ea69";
        public const string guidSolutionExplorerOpenOnGitHubCmdSetString = "006a78e9-9324-4388-8254-054dc01ddb59";
        private static readonly Guid guidOpenOnGitHubCmdSet = new(guidOpenOnGitHubCmdSetString);
        private static readonly Guid guidDocumentTabOpenOnGitHubCmdSet = new(guidDocumentTabOpenOnGitHubCmdSetString);
        private static readonly Guid guidSolutionExplorerOpenOnGitHubCmdSet = new(guidSolutionExplorerOpenOnGitHubCmdSetString);

        public static IEnumerable<Guid> EnumerateCmdSets()
        {
            yield return guidOpenOnGitHubCmdSet;
            yield return guidDocumentTabOpenOnGitHubCmdSet;
            yield return guidSolutionExplorerOpenOnGitHubCmdSet;
        }
    }

    static class PackageCommandIDs
    {
        public const int OpenMain = 0x100;
        public const int OpenBranch = 0x200;
        public const int OpenRevision = 0x300;
        public const int OpenRevisionFull = 0x400;

        public static IEnumerable<int> Enumerate()
        {
            yield return OpenMain;
            yield return OpenBranch;
            yield return OpenRevisionFull;
            yield return OpenRevision;
        }
    };

    static class PackageVersion
    {
        public const string Version = "2.0";
    }
}