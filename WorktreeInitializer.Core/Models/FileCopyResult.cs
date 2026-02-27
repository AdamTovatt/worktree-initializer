namespace WorktreeInitializer.Core.Models
{
    /// <summary>
    /// Represents the result of copying a single file.
    /// </summary>
    /// <param name="RelativePath">The relative path of the file.</param>
    /// <param name="SourcePath">The full source file path.</param>
    /// <param name="DestinationPath">The full destination file path.</param>
    /// <param name="Success">Whether the copy succeeded.</param>
    /// <param name="Error">The error message if the copy failed.</param>
    public record FileCopyResult(string RelativePath, string SourcePath, string DestinationPath, bool Success, string? Error = null);
}
