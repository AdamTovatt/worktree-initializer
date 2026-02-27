namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Represents the result of a command execution.
    /// </summary>
    /// <param name="Success">Indicates whether the command executed successfully.</param>
    /// <param name="Message">The main message to display to the user.</param>
    /// <param name="Details">Optional detailed information about the result.</param>
    public record CommandResult(bool Success, string Message, string? Details = null);
}
