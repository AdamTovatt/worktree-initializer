using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Commands
{
    /// <summary>
    /// Displays help information.
    /// </summary>
    public class HelpCommand : ICommand
    {
        public Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            string message = "WorktreeInitializer (wi) - Copy gitignored files to a new worktree";
            string details = string.Join(Environment.NewLine,
                "Usage:",
                "  wi init <source-path> <destination-path>    Copy gitignored files from source to destination",
                "  wi help                                      Show this help message",
                "  wi --mcp                                     Start as MCP server",
                "",
                "The init command copies all git-ignored files (e.g. build outputs, .env files)",
                "from a source repository to a destination directory, preserving the directory structure.");

            return Task.FromResult(new CommandResult(Success: true, Message: message, Details: details));
        }
    }
}
