using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Copies files with directory creation. Returns result objects instead of throwing.
    /// </summary>
    public class FileCopyService : IFileCopyService
    {
        public async Task<FileCopyResult> CopyFileAsync(string relativePath, string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                string? destinationDir = Path.GetDirectoryName(destinationPath);
                if (destinationDir != null)
                {
                    Directory.CreateDirectory(destinationDir);
                }

                using FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
                using FileStream destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);

                return new FileCopyResult(relativePath, sourcePath, destinationPath, Success: true);
            }
            catch (Exception ex)
            {
                return new FileCopyResult(relativePath, sourcePath, destinationPath, Success: false, Error: ex.Message);
            }
        }
    }
}
