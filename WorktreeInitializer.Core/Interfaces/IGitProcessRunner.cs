namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Runs git commands as subprocesses and returns their output.
    /// </summary>
    public interface IGitProcessRunner
    {
        /// <summary>
        /// Runs a git command and returns its stdout output.
        /// </summary>
        /// <param name="workingDirectory">The working directory for the git process.</param>
        /// <param name="arguments">The arguments to pass to git.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The trimmed stdout output.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when git is not available, the process fails to start,
        /// or the command exits with a non-zero exit code.
        /// </exception>
        Task<string> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken);
    }
}
