using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Runs a shell command as a subprocess and returns how it went.
    /// </summary>
    public interface IShellCommandRunner
    {
        /// <summary>
        /// Runs a command through the platform shell and waits for it to finish.
        /// </summary>
        /// <param name="workingDirectory">The working directory for the process.</param>
        /// <param name="command">The command line to run, interpreted by the shell.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A result carrying the exit code and the combined output.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the shell cannot be started or the command exceeds the runner's timeout.
        /// </exception>
        Task<ShellCommandResult> RunAsync(string workingDirectory, string command, CancellationToken cancellationToken);
    }
}
