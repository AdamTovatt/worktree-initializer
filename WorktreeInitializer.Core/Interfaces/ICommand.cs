namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Represents a command that can be executed.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The result of the command execution.</returns>
        Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken);
    }
}
