namespace WorktreeInitializer.Core.Models
{
    /// <summary>
    /// Represents the aggregate result of initializing a worktree.
    /// </summary>
    /// <param name="TotalFiles">The total number of ignored files found.</param>
    /// <param name="FilesCopied">The number of files successfully copied.</param>
    /// <param name="FilesFailed">The number of files that failed to copy.</param>
    /// <param name="Results">The individual file copy results.</param>
    public record InitializationResult(int TotalFiles, int FilesCopied, int FilesFailed, List<FileCopyResult> Results);
}
