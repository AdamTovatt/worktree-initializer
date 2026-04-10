using System.Diagnostics;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Runs git commands as subprocesses. Centralizes process spawning, error handling, and timeouts.
    /// </summary>
    public class GitProcessRunner : IGitProcessRunner
    {
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

        public async Task<string> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Failed to start git process.");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Failed to run git. Is git installed and in PATH? {ex.Message}", ex);
            }

            using (process)
            {
                using CancellationTokenSource timeoutCts = new CancellationTokenSource(ProcessTimeout);
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
                    await Task.WhenAll(stdoutTask, stderrTask);

                    string output = stdoutTask.Result;
                    string error = stderrTask.Result;

                    await process.WaitForExitAsync(linkedCts.Token);

                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"git exited with code {process.ExitCode}: {error.Trim()}");
                    }

                    return output;
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    try { process.Kill(); } catch { }
                    throw new InvalidOperationException($"git command timed out after {ProcessTimeout.TotalSeconds} seconds: git {arguments}");
                }
            }
        }
    }
}
