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
        private readonly IWorktreeConfigProvider _configProvider;
        private readonly IWorktreeDetector _worktreeDetector;

        public McpTools(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            IWorktreeConfigProvider configProvider,
            IWorktreeDetector worktreeDetector)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _configProvider = configProvider;
            _worktreeDetector = worktreeDetector;
        }

        [McpServerTool(Name = "wi_init")]
        [Description("Copy all gitignored files from a source git repository to a destination directory. Preserves directory structure. Auto-detection (omitting paths) only works when the MCP server was launched from inside a worktree. When calling from an external client, always provide both sourcePath and destinationPath explicitly.")]
        public async Task<string> InitAsync(
            CancellationToken cancellationToken,
            [Description("The path to the source git repository. Required when calling from an MCP client. If omitted, auto-detected from the server's working directory.")]
            string? sourcePath = null,
            [Description("The path to the destination directory (the worktree). Required when calling from an MCP client. If omitted, defaults to the server's working directory.")]
            string? destinationPath = null,
            [Description("Optional list of path prefixes to exclude from copying (e.g. node_modules, .venv)")]
            string[]? ignorePaths = null,
            [Description("Optional list of path prefixes to re-include even if they appear in ignore lists (include wins over ignore)")]
            string[]? includePaths = null)
        {
            string resolvedSource;
            string resolvedDestination;

            if (sourcePath != null && destinationPath != null)
            {
                resolvedSource = sourcePath;
                resolvedDestination = destinationPath;
            }
            else
            {
                try
                {
                    string cwd = Directory.GetCurrentDirectory();
                    resolvedSource = sourcePath ?? await _worktreeDetector.DetectSourceRepoAsync(cwd, cancellationToken);
                    resolvedDestination = destinationPath ?? cwd;
                }
                catch (InvalidOperationException ex)
                {
                    return $"Auto-detection failed: {ex.Message}";
                }
            }

            IReadOnlyList<string>? ignoreList = ignorePaths is { Length: > 0 } ? ignorePaths : null;
            IReadOnlyList<string>? includeList = includePaths is { Length: > 0 } ? includePaths : null;
            InitCommand command = new InitCommand(
                _gitIgnoredFileProvider, _pathMapper, _fileCopyService, _configProvider,
                resolvedSource, resolvedDestination, ignoreList, includeList);
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
