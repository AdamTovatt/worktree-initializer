using System.Diagnostics;
using System.Text.RegularExpressions;
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
                Arguments = "ls-files --others --ignored --exclude-standard -z",
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

                return ParseOutput(output);
            }
        }
        public static List<string> ParseOutput(string output)
        {
            List<string> files = output
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Regex.Replace(path.Replace('\\', '/'), "/+", "/"))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToList();

            return files;
        }
    }
}
