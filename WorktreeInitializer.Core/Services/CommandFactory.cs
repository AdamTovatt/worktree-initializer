using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Parses command-line arguments and creates the appropriate command.
    /// </summary>
    public class CommandFactory : ICommandFactory
    {
        private readonly IGitIgnoredFileProvider _gitIgnoredFileProvider;
        private readonly IPathMapper _pathMapper;
        private readonly IFileCopyService _fileCopyService;
        private readonly IWorktreeConfigProvider _configProvider;
        private readonly IShellCommandRunner _shellCommandRunner;
        private readonly IWorktreeDetector _worktreeDetector;
        private readonly Func<string> _getWorkingDirectory;

        public CommandFactory(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            IWorktreeConfigProvider configProvider,
            IShellCommandRunner shellCommandRunner,
            IWorktreeDetector worktreeDetector,
            Func<string>? getWorkingDirectory = null)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _configProvider = configProvider;
            _shellCommandRunner = shellCommandRunner;
            _worktreeDetector = worktreeDetector;
            _getWorkingDirectory = getWorkingDirectory ?? Directory.GetCurrentDirectory;
        }

        public ICommand CreateCommand(string[] args, IProgress<string>? progress = null)
        {
            if (args.Length == 0)
            {
                return new HelpCommand();
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "init":
                    return CreateInitCommand(args, progress);

                case "help":
                case "--help":
                case "-h":
                    return new HelpCommand();

                default:
                    throw new ArgumentException($"Unknown command: '{command}'. Run 'wi help' for usage information.");
            }
        }

        private ICommand CreateInitCommand(string[] args, IProgress<string>? progress)
        {
            string sourcePath;
            string destinationPath;
            int flagStartIndex;

            bool hasFirstPositional = args.Length >= 2 && !args[1].StartsWith("-");
            bool hasSecondPositional = args.Length >= 3 && !args[2].StartsWith("-");

            if (hasFirstPositional && hasSecondPositional)
            {
                // Explicit paths: wi init <source> <dest> [flags...]
                sourcePath = args[1];
                destinationPath = args[2];
                flagStartIndex = 3;
            }
            else if (!hasFirstPositional)
            {
                // No paths: wi init [flags...] — auto-detect
                string workingDirectory = _getWorkingDirectory();
                try
                {
                    sourcePath = _worktreeDetector
                        .DetectSourceRepoAsync(workingDirectory, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (InvalidOperationException ex)
                {
                    throw new ArgumentException(ex.Message, ex);
                }

                destinationPath = workingDirectory;
                flagStartIndex = 1;
            }
            else
            {
                // One positional arg — ambiguous
                throw new ArgumentException(
                    "Expected two paths or none.\n" +
                    "Usage: wi init <source-path> <destination-path>\n" +
                    "   or: wi init  (auto-detect when inside a worktree)");
            }

            (List<string> ignorePaths, List<string> includePaths) = ParseFlags(args, flagStartIndex);

            return new InitCommand(
                _gitIgnoredFileProvider,
                _pathMapper,
                _fileCopyService,
                _configProvider,
                _shellCommandRunner,
                sourcePath,
                destinationPath,
                ignorePaths.Count > 0 ? ignorePaths : null,
                includePaths.Count > 0 ? includePaths : null,
                progress);
        }

        private static (List<string> ignorePaths, List<string> includePaths) ParseFlags(string[] args, int startIndex)
        {
            List<string> ignorePaths = new List<string>();
            List<string> includePaths = new List<string>();

            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i] == "--ignore" && i + 1 < args.Length)
                {
                    ignorePaths.Add(args[i + 1]);
                    i++; // skip the value
                }
                else if (args[i] == "--include" && i + 1 < args.Length)
                {
                    includePaths.Add(args[i + 1]);
                    i++; // skip the value
                }
            }

            return (ignorePaths, includePaths);
        }
    }
}
