namespace WorktreeInitializer.Core.Models
{
    /// <summary>
    /// Represents the result of running a single shell command.
    /// </summary>
    /// <param name="Command">The command that was run, as written in the configuration.</param>
    /// <param name="ExitCode">The process exit code. -1 means the process could not be run at all.</param>
    /// <param name="Output">The combined stdout and stderr of the process, or the reason it could not be run.</param>
    public record ShellCommandResult(string Command, int ExitCode, string Output)
    {
        /// <summary>
        /// Whether the command completed with a zero exit code.
        /// </summary>
        public bool Success => ExitCode == 0;
    }
}
