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
                "  wi init <source-path> <destination-path> [--ignore <path>]... [--include <path>]...",
                "  wi help                                      Show this help message",
                "  wi --mcp                                     Start as MCP server",
                "",
                "Options:",
                "  --ignore <path>    Exclude files under <path> from copying (can be repeated)",
                "  --include <path>   Re-include files that would otherwise be ignored (wins over --ignore)",
                "",
                "The init command copies all git-ignored files (e.g. build outputs, .env files)",
                "from a source repository to a destination directory, preserving the directory structure.",
                "",
                "You can also place a WorktreeConfig.json in the source repo root to define",
                "default ignores:",
                "",
                "  {",
                "    \"ignores\": [\"node_modules\", \".venv\", \"dist\"]",
                "  }",
                "",
                "CLI --ignore flags are merged with config file ignores.",
                "--include overrides both CLI --ignore and config file ignores.");

            return Task.FromResult(new CommandResult(Success: true, Message: message, Details: details));
        }
    }
}
