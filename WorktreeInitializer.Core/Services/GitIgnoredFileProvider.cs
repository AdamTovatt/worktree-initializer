using System.Text.RegularExpressions;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Runs git to get the list of ignored files that exist on disk.
    /// </summary>
    public class GitIgnoredFileProvider : IGitIgnoredFileProvider
    {
        private readonly IGitProcessRunner _gitRunner;

        public GitIgnoredFileProvider(IGitProcessRunner gitRunner)
        {
            _gitRunner = gitRunner;
        }

        public async Task<List<string>> GetIgnoredFilesAsync(string repoPath, CancellationToken cancellationToken)
        {
            string output = await _gitRunner.RunAsync(repoPath, "ls-files --others --ignored --exclude-standard -z", cancellationToken);

            return ParseOutput(output);
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
