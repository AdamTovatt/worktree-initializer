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

        public CommandFactory(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            IWorktreeConfigProvider configProvider)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _configProvider = configProvider;
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
                    if (args.Length < 3)
                    {
                        throw new ArgumentException("Usage: wi init <source-path> <destination-path>");
                    }

                    List<string> ignorePaths = ParseIgnoreFlags(args, startIndex: 3);

                    return new InitCommand(
                        _gitIgnoredFileProvider,
                        _pathMapper,
                        _fileCopyService,
                        _configProvider,
                        args[1],
                        args[2],
                        ignorePaths.Count > 0 ? ignorePaths : null,
                        progress);

                case "help":
                case "--help":
                case "-h":
                    return new HelpCommand();

                default:
                    throw new ArgumentException($"Unknown command: '{command}'. Run 'wi help' for usage information.");
            }
        }

        private static List<string> ParseIgnoreFlags(string[] args, int startIndex)
        {
            List<string> ignorePaths = new List<string>();

            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i] == "--ignore" && i + 1 < args.Length)
                {
                    ignorePaths.Add(args[i + 1]);
                    i++; // skip the value
                }
            }

            return ignorePaths;
        }
    }
}
