using System.Diagnostics;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Runs shell commands as subprocesses. Centralizes process spawning, error handling, and timeouts.
    /// </summary>
    public class ShellCommandRunner : IShellCommandRunner
    {
        // Post-initialize commands restore dependency trees — a cold `npm install` or `dotnet restore`
        // is minutes of work, so this is far longer than the git runner's timeout.
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(30);

        public async Task<ShellCommandResult> RunAsync(string workingDirectory, string command, CancellationToken cancellationToken)
        {
            ProcessStartInfo startInfo = CreateStartInfo(workingDirectory, command);

            Process process;
            try
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException($"Failed to start a shell to run: {command}");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException($"Failed to run '{command}': {ex.Message}", ex);
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

                    await process.WaitForExitAsync(linkedCts.Token);

                    string output = CombineOutput(stdoutTask.Result, stderrTask.Result);

                    return new ShellCommandResult(command, process.ExitCode, output);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    throw new InvalidOperationException($"Command timed out after {ProcessTimeout.TotalMinutes} minutes: {command}");
                }
            }
        }

        private static ProcessStartInfo CreateStartInfo(string workingDirectory, string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(command);
            }
            else
            {
                startInfo.FileName = "/bin/sh";
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add(command);
            }

            return startInfo;
        }

        private static string CombineOutput(string standardOutput, string standardError)
        {
            string trimmedOutput = standardOutput.Trim();
            string trimmedError = standardError.Trim();

            if (trimmedOutput.Length == 0)
            {
                return trimmedError;
            }

            if (trimmedError.Length == 0)
            {
                return trimmedOutput;
            }

            return trimmedOutput + Environment.NewLine + trimmedError;
        }
    }
}
