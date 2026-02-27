using System.Diagnostics;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Runs git to get the list of ignored files that exist on disk.
    /// </summary>
    public class GitIgnoredFileProvider : IGitIgnoredFileProvider
    {
        public async Task<List<string>> GetIgnoredFilesAsync(string repoPath, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --others --ignored --exclude-standard",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start git process.");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Failed to run git. Is git installed and in PATH? {ex.Message}", ex);
            }

            using (process)
            {
                string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                string error = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"git exited with code {process.ExitCode}: {error.Trim()}");
                }

                List<string> files = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.TrimEnd('\r'))
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                return files;
            }
        }
    }
}
