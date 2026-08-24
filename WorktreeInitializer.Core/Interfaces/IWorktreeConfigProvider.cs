using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Reads the settings a source repository declares for worktree initialization.
    /// </summary>
    public interface IWorktreeConfigProvider
    {
        /// <summary>
        /// Reads the source repository's WorktreeConfig.json.
        /// </summary>
        /// <param name="repoPath">The source repository root to read the file from.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The declared settings, or <see cref="WorktreeConfig.Empty"/> when there is no config file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the config file exists but cannot be parsed.</exception>
        Task<WorktreeConfig> GetConfigAsync(string repoPath, CancellationToken cancellationToken);
    }
}
