using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Commands
{
    /// <summary>
    /// Orchestrates copying gitignored files from source to destination.
    /// </summary>
    public class InitCommand : ICommand
    {
        private readonly IGitIgnoredFileProvider _gitIgnoredFileProvider;
        private readonly IPathMapper _pathMapper;
        private readonly IFileCopyService _fileCopyService;
        private readonly string _sourcePath;
        private readonly string _destinationPath;

        public InitCommand(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            string sourcePath,
            string destinationPath)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
        }

        public async Task<CommandResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            string resolvedSource = Path.GetFullPath(_sourcePath);
            string resolvedDestination = Path.GetFullPath(_destinationPath);

            if (!Directory.Exists(resolvedSource))
            {
                return new CommandResult(Success: false, Message: $"Source directory does not exist: {resolvedSource}");
            }

            if (!Directory.Exists(resolvedDestination))
            {
                return new CommandResult(Success: false, Message: $"Destination directory does not exist: {resolvedDestination}");
            }

            List<string> ignoredFiles;
            try
            {
                ignoredFiles = await _gitIgnoredFileProvider.GetIgnoredFilesAsync(resolvedSource, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return new CommandResult(Success: false, Message: $"Failed to get ignored files: {ex.Message}");
            }

            if (ignoredFiles.Count == 0)
            {
                return new CommandResult(Success: true, Message: "Nothing to copy — no ignored files found.");
            }

            List<FileCopyResult> results = new List<FileCopyResult>();

            foreach (string relativePath in ignoredFiles)
            {
                string sourceFullPath = _pathMapper.MapToFullPath(relativePath, resolvedSource);
                string destFullPath = _pathMapper.MapToFullPath(relativePath, resolvedDestination);
                FileCopyResult result = await _fileCopyService.CopyFileAsync(relativePath, sourceFullPath, destFullPath, cancellationToken);
                results.Add(result);
            }

            int copied = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success);
            InitializationResult initResult = new InitializationResult(ignoredFiles.Count, copied, failed, results);

            string message = failed == 0
                ? $"Successfully copied {copied} file(s)."
                : $"Copied {copied} file(s), {failed} failed.";

            string? details = null;
            if (failed > 0)
            {
                IEnumerable<string> failureLines = results
                    .Where(r => !r.Success)
                    .Select(r => $"  FAILED: {r.RelativePath} — {r.Error}");
                details = string.Join(Environment.NewLine, failureLines);
            }

            return new CommandResult(Success: failed == 0, Message: message, Details: details);
        }
    }
}
