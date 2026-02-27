using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Copies files from source to destination with directory creation.
    /// </summary>
    public interface IFileCopyService
    {
        /// <summary>
        /// Copies a file from source to destination, creating directories as needed.
        /// Returns a result object instead of throwing on failure.
        /// </summary>
        /// <param name="relativePath">The relative path of the file.</param>
        /// <param name="sourcePath">The full source file path.</param>
        /// <param name="destinationPath">The full destination file path.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<FileCopyResult> CopyFileAsync(string relativePath, string sourcePath, string destinationPath, CancellationToken cancellationToken);
    }
}
