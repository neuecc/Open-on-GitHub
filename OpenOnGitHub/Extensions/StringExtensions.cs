﻿using System;
using System.IO;
using System.Linq;

namespace OpenOnGitHub.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="path"/> starts with the path <paramref name="baseDirPath"/>.
        /// The comparison is case-insensitive, handles / and \ slashes as folder separators and
        /// only matches if the base dir folder name is matched exactly ("c:\foobar\file.txt" is not a sub path of "c:\foo").
        /// </summary>
        public static bool IsSubPathOf(this string path, string baseDirPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(baseDirPath))
            {
                return false;
            }

            var normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
                .WithEnding("\\"));

            var normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
                .WithEnding("\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Appends path segments to a base URL, ensuring correct slash formatting.
        /// </summary>
        /// <param name="baseUrl">The base URL to which path segments should be appended.</param>
        /// <param name="segments">An array of path segments to append.</param>
        /// <returns>The full URL with properly formatted path segments.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseUrl"/> is null or empty.</exception>
        public static string AppendUriPathSegments(this string baseUrl, params string[] segments)
        {
            var trimmedBase = baseUrl?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(trimmedBase))
                throw new ArgumentNullException(nameof(baseUrl));

            var cleanedSegments = segments
                .Select(s => s?.Trim('/'))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            cleanedSegments.Insert(0, trimmedBase);

            return string.Join("/", cleanedSegments);
        }

        /// <summary>
        /// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
        /// results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
        private static string WithEnding(this string str, string ending)
        {
            if (str == null)
                return ending;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (var i = 0; i <= ending.Length; i++)
            {
                var tmp = str + ending.Right(i);
                if (tmp.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                    return tmp;
            }

            return str;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        private static string Right(this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length is less than zero");
            }

            return length < value.Length ? value.Substring(value.Length - length) : value;
        }
    }
}
