using System;
using System.Collections.Generic;

namespace OpenOnGitHub
{
    static class PackageGuids
    {
        public const string guidOpenOnGitHubPkgString = "465b40b6-311a-4e37-9556-95fced2de9c6";
        private const string guidOpenOnGitHubCmdSetString = "a674aaec-a6f5-4df0-9749-e7bef776df5d";
        public static readonly Guid guidOpenOnGitHubCmdSet = new(guidOpenOnGitHubCmdSetString);
    };

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