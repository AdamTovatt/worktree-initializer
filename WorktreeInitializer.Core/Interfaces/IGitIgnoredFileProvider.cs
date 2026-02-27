namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Provides a list of git-ignored files that exist on disk in a repository.
    /// </summary>
    public interface IGitIgnoredFileProvider
    {
        /// <summary>
        /// Gets the relative paths of all ignored files in the specified repository.
        /// </summary>
        /// <param name="repoPath">The path to the git repository.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A list of relative file paths (using forward slashes as git outputs them).</returns>
        /// <exception cref="InvalidOperationException">Thrown if git is not available or the path is not a git repository.</exception>
        Task<List<string>> GetIgnoredFilesAsync(string repoPath, CancellationToken cancellationToken);
    }
}
