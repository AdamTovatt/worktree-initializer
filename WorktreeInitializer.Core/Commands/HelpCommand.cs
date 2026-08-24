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
                "  wi init [options]                                    Auto-detect source from worktree",
                "  wi init <source-path> <destination-path> [options]   Explicit source and destination",
                "  wi help                                              Show this help message",
                "  wi --mcp                                             Start as MCP server",
                "",
                "Options:",
                "  --ignore <path>    Exclude files under <path> from copying (can be repeated)",
                "  --include <path>   Re-include files that would otherwise be ignored (wins over --ignore)",
                "",
                "The init command copies all git-ignored files (e.g. build outputs, .env files)",
                "from a source repository to a destination directory, preserving the directory structure.",
                "",
                "A symbolic link is recreated as a link to the same target rather than having its",
                "content copied, and a regular file keeps its permission bits.",
                "",
                "When run inside a git worktree with no paths, 'wi init' automatically detects",
                "the main repository as the source and uses the current directory as the destination.",
                "",
                "You can also place a WorktreeConfig.json in the source repo root to define",
                "default ignores and commands to run once the copy has finished:",
                "",
                "  {",
                "    \"ignores\": [\"node_modules\", \".venv\", \"dist\"],",
                "    \"postInitialize\": [\"npm install\"]",
                "  }",
                "",
                "CLI --ignore flags are merged with config file ignores.",
                "--include overrides both CLI --ignore and config file ignores.",
                "",
                "postInitialize commands run in the destination worktree, in order, through the",
                "platform shell. The first one to exit non-zero stops the rest and fails the init.");

            return Task.FromResult(new CommandResult(Success: true, Message: message, Details: details));
        }
    }
}
