using System;

namespace NexusDash.Models
{
    public sealed record FileSearchResult(
        string Name,
        string DirectoryPath,
        string FullPath,
        bool IsDirectory,
        long? SizeBytes,
        DateTime? LastWriteTime);
}
