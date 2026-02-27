using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WorktreeInitializer.Cli
{
    /// <summary>
    /// MCP tools for worktree initialization.
    /// </summary>
    [McpServerToolType]
    public class McpTools
    {
        private readonly IGitIgnoredFileProvider _gitIgnoredFileProvider;
        private readonly IPathMapper _pathMapper;
        private readonly IFileCopyService _fileCopyService;

        public McpTools(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
        }

        [McpServerTool(Name = "wi_init")]
        [Description("Copy all gitignored files from a source git repository to a destination directory. Preserves directory structure. Useful for initializing a new worktree with build outputs, .env files, and other ignored files.")]
        public async Task<string> InitAsync(
            [Description("The path to the source git repository")]
            string sourcePath,
            [Description("The path to the destination directory")]
            string destinationPath,
            CancellationToken cancellationToken)
        {
            InitCommand command = new InitCommand(_gitIgnoredFileProvider, _pathMapper, _fileCopyService, sourcePath, destinationPath);
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        [McpServerTool(Name = "wi_help")]
        [Description("Show usage information for all WorktreeInitializer tools.")]
        public async Task<string> GetHelpAsync(CancellationToken cancellationToken)
        {
            HelpCommand command = new HelpCommand();
            CommandResult result = await command.ExecuteAsync(cancellationToken);
            return FormatResult(result);
        }

        private static string FormatResult(CommandResult result)
        {
            if (string.IsNullOrEmpty(result.Details))
            {
                return result.Message;
            }

            return $"{result.Message}\n\n{result.Details}";
        }
    }
}
