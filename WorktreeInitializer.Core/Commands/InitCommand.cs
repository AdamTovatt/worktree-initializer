using System.Diagnostics;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Commands
{
    /// <summary>
    /// Orchestrates copying gitignored files from source to destination, then runs the
    /// source repository's declared post-initialize commands in the destination.
    /// </summary>
    public class InitCommand : ICommand
    {
        private readonly IGitIgnoredFileProvider _gitIgnoredFileProvider;
        private readonly IPathMapper _pathMapper;
        private readonly IFileCopyService _fileCopyService;
        private readonly IWorktreeConfigProvider _configProvider;
        private readonly IShellCommandRunner _shellCommandRunner;
        private readonly string _sourcePath;
        private readonly string _destinationPath;
        private readonly IReadOnlyList<string>? _ignorePaths;
        private readonly IReadOnlyList<string>? _includePaths;
        private readonly IProgress<string>? _progress;

        public InitCommand(
            IGitIgnoredFileProvider gitIgnoredFileProvider,
            IPathMapper pathMapper,
            IFileCopyService fileCopyService,
            IWorktreeConfigProvider configProvider,
            IShellCommandRunner shellCommandRunner,
            string sourcePath,
            string destinationPath,
            IReadOnlyList<string>? ignorePaths = null,
            IReadOnlyList<string>? includePaths = null,
            IProgress<string>? progress = null)
        {
            _gitIgnoredFileProvider = gitIgnoredFileProvider;
            _pathMapper = pathMapper;
            _fileCopyService = fileCopyService;
            _configProvider = configProvider;
            _shellCommandRunner = shellCommandRunner;
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _ignorePaths = ignorePaths;
            _includePaths = includePaths;
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

            // Read before discovering files: the post-initialize commands run even when there is
            // nothing to copy, and a malformed config should fail before any work is done.
            WorktreeConfig config;
            try
            {
                config = await _configProvider.GetConfigAsync(resolvedSource, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return new CommandResult(Success: false, Message: $"Failed to read config: {ex.Message}");
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

            HashSet<string> ignorePatterns = BuildIgnorePatterns(config);

            // Filter out files matching ignore patterns
            int skippedCount = 0;
            if (ignorePatterns.Count > 0)
            {
                int beforeCount = ignoredFiles.Count;
                ignoredFiles = ignoredFiles.Where(f => !IsIgnored(f, ignorePatterns)).ToList();
                skippedCount = beforeCount - ignoredFiles.Count;
            }

            string copyMessage;
            string? copyDetails = null;
            int failed = 0;

            if (ignoredFiles.Count == 0)
            {
                string skipInfo = skippedCount > 0 ? $" ({skippedCount} file(s) excluded by ignore rules)" : "";
                copyMessage = $"Nothing to copy — no ignored files found.{skipInfo}";
            }
            else
            {
                if (skippedCount > 0)
                {
                    _progress?.Report($"Discovered {ignoredFiles.Count} file(s) to copy ({skippedCount} excluded by ignore rules).");
                }
                else
                {
                    _progress?.Report($"Discovered {ignoredFiles.Count} file(s) to copy.");
                }

                List<FileCopyResult> results = await CopyFilesAsync(ignoredFiles, resolvedSource, resolvedDestination, cancellationToken);

                int successCount = results.Count(r => r.Success);
                failed = results.Count - successCount;

                copyMessage = failed == 0
                    ? $"Successfully copied {successCount} file(s)."
                    : $"Copied {successCount} file(s), {failed} failed.";

                if (failed > 0)
                {
                    IEnumerable<string> failureLines = results
                        .Where(r => !r.Success)
                        .Select(r => $"  FAILED: {r.RelativePath} — {r.Error}");
                    copyDetails = string.Join(Environment.NewLine, failureLines);
                }
            }

            List<ShellCommandResult> commandResults = await RunPostInitializeCommandsAsync(
                config.PostInitializeCommands, resolvedDestination, cancellationToken);

            return BuildResult(copyMessage, copyDetails, failed, config.PostInitializeCommands, commandResults);
        }

        private HashSet<string> BuildIgnorePatterns(WorktreeConfig config)
        {
            // Merge CLI ignore paths + config file ignore patterns
            HashSet<string> ignorePatterns = new HashSet<string>(StringComparer.Ordinal);

            if (_ignorePaths != null)
            {
                foreach (string pattern in _ignorePaths)
                {
                    ignorePatterns.Add(pattern);
                }
            }

            foreach (string pattern in config.Ignores)
            {
                ignorePatterns.Add(pattern);
            }

            // --include wins: remove any patterns that appear in the include list
            if (_includePaths != null)
            {
                foreach (string pattern in _includePaths)
                {
                    ignorePatterns.Remove(pattern);
                }
            }

            return ignorePatterns;
        }

        private async Task<List<FileCopyResult>> CopyFilesAsync(
            List<string> relativePaths,
            string resolvedSource,
            string resolvedDestination,
            CancellationToken cancellationToken)
        {
            List<FileCopyResult> results = new List<FileCopyResult>();
            int copied = 0;
            int total = relativePaths.Count;
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (string relativePath in relativePaths)
            {
                string sourceFullPath = _pathMapper.MapToFullPath(relativePath, resolvedSource);
                string destFullPath = _pathMapper.MapToFullPath(relativePath, resolvedDestination);
                FileCopyResult result = await _fileCopyService.CopyFileAsync(relativePath, sourceFullPath, destFullPath, cancellationToken);
                results.Add(result);
                copied++;

                if (_progress != null && stopwatch.Elapsed.TotalSeconds >= 3)
                {
                    int percent = (int)((long)copied * 100 / total);
                    _progress.Report($"{percent}% — {copied}/{total} files copied — {relativePath}");
                    stopwatch.Restart();
                }
            }

            return results;
        }

        private async Task<List<ShellCommandResult>> RunPostInitializeCommandsAsync(
            IReadOnlyList<string> commands,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            List<ShellCommandResult> results = new List<ShellCommandResult>();

            foreach (string command in commands)
            {
                _progress?.Report($"Running post-initialize command: {command}");

                ShellCommandResult result;
                try
                {
                    result = await _shellCommandRunner.RunAsync(workingDirectory, command, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    result = new ShellCommandResult(command, ExitCode: -1, Output: ex.Message);
                }

                results.Add(result);

                if (!result.Success)
                {
                    // Later commands are written assuming the earlier ones succeeded.
                    break;
                }
            }

            return results;
        }

        private static CommandResult BuildResult(
            string copyMessage,
            string? copyDetails,
            int filesFailed,
            IReadOnlyList<string> requestedCommands,
            List<ShellCommandResult> commandResults)
        {
            if (requestedCommands.Count == 0)
            {
                return new CommandResult(Success: filesFailed == 0, Message: copyMessage, Details: copyDetails);
            }

            ShellCommandResult? failedCommand = commandResults.FirstOrDefault(r => !r.Success);

            if (failedCommand == null)
            {
                string message = $"{copyMessage} Ran {commandResults.Count} post-initialize command(s).";
                return new CommandResult(Success: filesFailed == 0, Message: message, Details: copyDetails);
            }

            int notRun = requestedCommands.Count - commandResults.Count;
            string skippedInfo = notRun > 0 ? $" {notRun} later command(s) were not run." : "";

            List<string> detailLines = new List<string>();
            if (copyDetails != null)
            {
                detailLines.Add(copyDetails);
            }

            detailLines.Add($"  POST-INITIALIZE FAILED (exit code {failedCommand.ExitCode}): {failedCommand.Command}");
            if (failedCommand.Output.Length > 0)
            {
                detailLines.Add(failedCommand.Output);
            }

            return new CommandResult(
                Success: false,
                Message: $"{copyMessage} Post-initialize command failed: {failedCommand.Command}.{skippedInfo}",
                Details: string.Join(Environment.NewLine, detailLines));
        }

        private static bool IsIgnored(string relativePath, HashSet<string> ignorePatterns)
        {
            foreach (string pattern in ignorePatterns)
            {
                if (relativePath == pattern || relativePath.StartsWith(pattern + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
