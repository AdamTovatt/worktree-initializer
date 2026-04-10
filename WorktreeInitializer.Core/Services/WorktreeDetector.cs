using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Detects the main repository root by running git rev-parse --git-common-dir.
    /// </summary>
    public class WorktreeDetector : IWorktreeDetector
    {
        private readonly IGitProcessRunner _gitRunner;

        public WorktreeDetector(IGitProcessRunner gitRunner)
        {
            _gitRunner = gitRunner;
        }

        public async Task<string> DetectSourceRepoAsync(string workingDirectory, CancellationToken cancellationToken)
        {
            string output;
            try
            {
                output = await _gitRunner.RunAsync(workingDirectory, "rev-parse --git-common-dir", cancellationToken);
            }
            catch (InvalidOperationException ex) when (!ex.Message.Contains("Is git installed"))
            {
                throw new InvalidOperationException(
                    "Not inside a git repository. " +
                    "Use explicit paths: wi init <source-path> <destination-path>", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"{ex.Message} Use explicit paths: wi init <source-path> <destination-path>", ex);
            }

            string gitCommonDir = output.Trim();
            string repoRoot = ResolveRepositoryRoot(workingDirectory, gitCommonDir);
            string normalizedRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar);
            string normalizedCwd = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar);

            if (string.Equals(normalizedRoot, normalizedCwd, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Current directory is the main repository, not a worktree. " +
                    "Use explicit paths: wi init <source-path> <destination-path>");
            }

            if (!Directory.Exists(normalizedRoot))
            {
                throw new InvalidOperationException(
                    $"Detected source repository '{normalizedRoot}' does not exist. " +
                    "Use explicit paths: wi init <source-path> <destination-path>");
            }

            return normalizedRoot;
        }

        private static string ResolveRepositoryRoot(string workingDirectory, string gitCommonDir)
        {
            string absoluteGitDir;
            if (Path.IsPathRooted(gitCommonDir))
            {
                absoluteGitDir = gitCommonDir;
            }
            else
            {
                absoluteGitDir = Path.GetFullPath(Path.Combine(workingDirectory, gitCommonDir));
            }

            string normalized = Path.GetFullPath(absoluteGitDir);

            // If it ends with ".git", the parent is the repository root
            if (Path.GetFileName(normalized) == ".git")
            {
                string? parent = Path.GetDirectoryName(normalized);
                if (parent != null)
                {
                    return parent;
                }
            }

            // Handle paths like "/home/pi/code/ordo/.git/worktrees/something"
            int gitIndex = normalized.IndexOf(
                $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal);

            if (gitIndex >= 0)
            {
                return normalized[..gitIndex];
            }

            throw new InvalidOperationException(
                $"Unexpected git-common-dir format: '{gitCommonDir}'. " +
                "Use explicit paths: wi init <source-path> <destination-path>");
        }
    }
}
