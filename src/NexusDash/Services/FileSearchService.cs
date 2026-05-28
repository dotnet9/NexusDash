using NexusDash.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.Services
{
    public sealed class FileSearchService
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private static readonly EnumerationOptions SearchEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        public IReadOnlyList<string> GetDefaultSearchRoots()
        {
            var driveRoots = DriveInfo.GetDrives()
                .Where(static drive => drive.IsReady &&
                                       (drive.DriveType == DriveType.Fixed ||
                                        drive.DriveType == DriveType.Removable))
                .Select(static drive => drive.RootDirectory.FullName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (driveRoots.Length > 0)
            {
                return driveRoots;
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(userProfile)
                ? [userProfile]
                : [Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory];
        }

        public Task<int> SearchByFileNameAsync(
            string query,
            IReadOnlyList<string> roots,
            int maxResults,
            Action<FileSearchResult> resultFound,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => SearchByFileName(query, roots, maxResults, resultFound, cancellationToken),
                cancellationToken);
        }

        private static int SearchByFileName(
            string query,
            IReadOnlyList<string> roots,
            int maxResults,
            Action<FileSearchResult> resultFound,
            CancellationToken cancellationToken)
        {
            var trimmedQuery = query.Trim();
            if (trimmedQuery.Length == 0 || maxResults <= 0)
            {
                return 0;
            }

            if (trimmedQuery.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                return 0;
            }

            var resultCount = 0;
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in NormalizeRoots(roots))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(root))
                {
                    TryAddMatchingResult(new FileInfo(root), trimmedQuery, ref resultCount, seenPaths, maxResults, resultFound);
                    continue;
                }

                if (!Directory.Exists(root))
                {
                    continue;
                }

                SearchDirectoryTree(
                    new DirectoryInfo(root),
                    trimmedQuery,
                    CreateMatchPattern(trimmedQuery),
                    ref resultCount,
                    seenPaths,
                    maxResults,
                    resultFound,
                    cancellationToken);

                if (resultCount >= maxResults)
                {
                    break;
                }
            }

            return resultCount;
        }

        private static string CreateMatchPattern(string query)
        {
            return $"*{query}*";
        }

        private static IEnumerable<string> NormalizeRoots(IReadOnlyList<string> roots)
        {
            foreach (var root in roots)
            {
                var normalized = Environment.ExpandEnvironmentVariables(root.Trim().Trim('"'));
                if (normalized.Length == 0)
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(normalized);
                }
                catch
                {
                    continue;
                }

                yield return fullPath;
            }
        }

        private static void SearchDirectoryTree(
            DirectoryInfo root,
            string query,
            string matchPattern,
            ref int resultCount,
            ISet<string> seenPaths,
            int maxResults,
            Action<FileSearchResult> resultFound,
            CancellationToken cancellationToken)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(root.FullName);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDirectory = pendingDirectories.Pop();

                foreach (var entry in EnumerateMatchingFileSystemInfos(currentDirectory, matchPattern))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryAddMatchingResult(entry, query, ref resultCount, seenPaths, maxResults, resultFound);
                    if (resultCount >= maxResults)
                    {
                        return;
                    }
                }

                foreach (var directory in EnumerateDirectories(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pendingDirectories.Push(directory.FullName);
                }
            }
        }

        private static IEnumerable<FileSystemInfo> EnumerateMatchingFileSystemInfos(string root, string matchPattern)
        {
            IEnumerator<FileSystemInfo>? enumerator = null;
            try
            {
                enumerator = new DirectoryInfo(root)
                    .EnumerateFileSystemInfos(matchPattern, SearchEnumerationOptions)
                    .GetEnumerator();
            }
            catch
            {
                yield break;
            }

            using (enumerator)
            {
                while (true)
                {
                    FileSystemInfo current;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            yield break;
                        }

                        current = enumerator.Current;
                    }
                    catch
                    {
                        yield break;
                    }

                    yield return current;
                }
            }
        }

        private static IEnumerable<DirectoryInfo> EnumerateDirectories(string root)
        {
            IEnumerator<DirectoryInfo>? enumerator = null;
            try
            {
                enumerator = new DirectoryInfo(root)
                    .EnumerateDirectories("*", SearchEnumerationOptions)
                    .GetEnumerator();
            }
            catch
            {
                yield break;
            }

            using (enumerator)
            {
                while (true)
                {
                    DirectoryInfo current;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            yield break;
                        }

                        current = enumerator.Current;
                    }
                    catch
                    {
                        yield break;
                    }

                    yield return current;
                }
            }
        }

        private static void TryAddMatchingResult(
            FileSystemInfo entry,
            string query,
            ref int resultCount,
            ISet<string> seenPaths,
            int maxResults,
            Action<FileSearchResult> resultFound)
        {
            if (resultCount >= maxResults)
            {
                return;
            }

            var name = entry.Name;
            if (name.Length == 0 ||
                name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            if (!seenPaths.Add(entry.FullName))
            {
                return;
            }

            resultCount++;
            resultFound(CreateResult(entry, name));
        }

        private static FileSearchResult CreateResult(FileSystemInfo entry, string name)
        {
            try
            {
                if (entry is DirectoryInfo directory)
                {
                    return new FileSearchResult(
                        name,
                        directory.Parent?.FullName ?? "",
                        directory.FullName,
                        IsDirectory: true,
                        SizeBytes: null,
                        directory.LastWriteTime);
                }

                var file = (FileInfo)entry;
                return new FileSearchResult(
                    name,
                    file.DirectoryName ?? "",
                    file.FullName,
                    IsDirectory: false,
                    file.Length,
                    file.LastWriteTime);
            }
            catch
            {
                return new FileSearchResult(
                    name,
                    Path.GetDirectoryName(entry.FullName) ?? "",
                    entry.FullName,
                    IsDirectory: false,
                    SizeBytes: null,
                    LastWriteTime: null);
            }
        }
    }
}
