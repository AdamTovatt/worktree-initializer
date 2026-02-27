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

        public CommandFactory(IGitIgnoredFileProvider gitIgnoredFileProvider, IPathMapper pathMapper, IFileCopyService fileCopyService)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
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
                    return new InitCommand(_gitIgnoredFileProvider, _pathMapper, _fileCopyService, args[1], args[2], progress);

                case "help":
                case "--help":
                case "-h":
                    return new HelpCommand();

                default:
                    throw new ArgumentException($"Unknown command: '{command}'. Run 'wi help' for usage information.");
            }
        }
    }
}
