using System.Diagnostics;
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
        private readonly IProgress<string>? _progress;

        public InitCommand(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            string sourcePath,
            string destinationPath,
            IProgress<string>? progress = null)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _progress = progress;
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

            _progress?.Report("Discovering files to copy...");

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

            _progress?.Report($"Discovered {ignoredFiles.Count} file(s) to copy.");

            List<FileCopyResult> results = new List<FileCopyResult>();
            int copied = 0;
            int total = ignoredFiles.Count;
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (string relativePath in ignoredFiles)
            {
                string sourceFullPath = _pathMapper.MapToFullPath(relativePath, resolvedSource);
                string destFullPath = _pathMapper.MapToFullPath(relativePath, resolvedDestination);
                FileCopyResult result = await _fileCopyService.CopyFileAsync(relativePath, sourceFullPath, destFullPath, cancellationToken);
                results.Add(result);
                copied++;

                if (_progress != null && stopwatch.Elapsed.TotalSeconds >= 3)
                {
                    int percent = (int)((long)copied * 100 / total);
                    _progress.Report($"{percent}% — {copied}/{total} files copied \u2014 {relativePath}");
                    stopwatch.Restart();
                }
            }

            int successCount = results.Count(r => r.Success);
            int failed = results.Count(r => !r.Success);
            InitializationResult initResult = new InitializationResult(ignoredFiles.Count, successCount, failed, results);

            string message = failed == 0
                ? $"Successfully copied {successCount} file(s)."
                : $"Copied {successCount} file(s), {failed} failed.";

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
