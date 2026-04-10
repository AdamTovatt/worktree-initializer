namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Detects the source (main) repository root when running inside a git worktree.
    /// </summary>
    public interface IWorktreeDetector
    {
        /// <summary>
        /// Resolves the root of the main repository from the given working directory.
        /// </summary>
        /// <param name="workingDirectory">The directory to detect from (typically cwd).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The absolute path to the main repository root.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when detection fails: git not available, not in a git repo,
        /// or currently in the main repo (not a worktree).
        /// </exception>
        Task<string> DetectSourceRepoAsync(string workingDirectory, CancellationToken cancellationToken);
    }
}
