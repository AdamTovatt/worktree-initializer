using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Copies files with directory creation. Returns result objects instead of throwing.
    /// A symbolic link is recreated as a link to the same raw target instead of having the
    /// linked content copied, and a regular file keeps its unix permission bits. Copying a
    /// link's content instead of the link is what turns a package manager's bin shims into
    /// plain non-executable files, and makes a link to a directory impossible to copy at all.
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

                string? linkTarget = new FileInfo(sourcePath).LinkTarget;
                if (linkTarget != null)
                {
                    await CopyAsSymbolicLinkAsync(sourcePath, destinationPath, linkTarget, cancellationToken);
                    return new FileCopyResult(relativePath, sourcePath, destinationPath, Success: true);
                }

                if (new FileInfo(destinationPath).LinkTarget != null)
                {
                    // Opening a link for writing writes through it, changing whatever it points at
                    // rather than replacing the link with the file being copied.
                    File.Delete(destinationPath);
                }

                await CopyFileContentAsync(sourcePath, destinationPath, cancellationToken);
                CopyUnixFileMode(sourcePath, destinationPath);

                return new FileCopyResult(relativePath, sourcePath, destinationPath, Success: true);
            }
            catch (Exception ex)
            {
                return new FileCopyResult(relativePath, sourcePath, destinationPath, Success: false, Error: ex.Message);
            }
        }

        private static async Task CopyAsSymbolicLinkAsync(string sourcePath, string destinationPath, string linkTarget, CancellationToken cancellationToken)
        {
            // The raw target is recreated verbatim, so a relative link keeps pointing inside the
            // destination tree rather than back at the source repository.
            DeleteExistingDestination(destinationPath);

            try
            {
                File.CreateSymbolicLink(destinationPath, linkTarget);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Creating a link can be denied outright (Windows without the privilege). A link to a
                // file still has content worth copying; a link to a directory does not, so it fails.
                if (!File.Exists(sourcePath))
                {
                    throw;
                }

                await CopyFileContentAsync(sourcePath, destinationPath, cancellationToken);
            }
        }

        private static async Task CopyFileContentAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            using FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            using FileStream destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }

        private static void DeleteExistingDestination(string destinationPath)
        {
            if (new FileInfo(destinationPath).LinkTarget != null || File.Exists(destinationPath))
            {
                // Deleting a link to a directory unlinks the link and leaves the directory alone.
                File.Delete(destinationPath);
                return;
            }

            if (Directory.Exists(destinationPath))
            {
                // Non-recursive on purpose: a non-empty directory here is not something this tool
                // created, and the resulting error is reported rather than silently destroying it.
                Directory.Delete(destinationPath, recursive: false);
            }
        }

        private static void CopyUnixFileMode(string sourcePath, string destinationPath)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            // Only called for regular files: these follow links, so calling them on a link would
            // change the permissions of the link's target instead.
            File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(sourcePath));
        }
    }
}
