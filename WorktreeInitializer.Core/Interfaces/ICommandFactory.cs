namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Factory for creating command instances from arguments.
    /// </summary>
    public interface ICommandFactory
    {
        /// <summary>
        /// Creates a command from the specified arguments.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The created command.</returns>
        /// <exception cref="ArgumentException">Thrown if the arguments are invalid or the command is unknown.</exception>
        ICommand CreateCommand(string[] args);
    }
}
