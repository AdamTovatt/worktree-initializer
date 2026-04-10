using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class WorktreeDetectorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly WorktreeDetector _detector = new WorktreeDetector(new GitProcessRunner());

        public WorktreeDetectorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            ForceDeleteDirectory(_tempDir);
        }

        private static void ForceDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;

            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }

        private async Task RunGitAsync(string workingDir, string arguments)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi)!;
            await process.WaitForExitAsync();
        }

        private async Task<(string mainRepo, string worktree)> CreateGitRepoWithWorktreeAsync()
        {
            string mainRepo = Path.Combine(_tempDir, "main-repo");
            string worktree = Path.Combine(_tempDir, "worktree");
            Directory.CreateDirectory(mainRepo);

            await RunGitAsync(mainRepo, "init");
            await RunGitAsync(mainRepo, "config user.email test@test.com");
            await RunGitAsync(mainRepo, "config user.name Test");

            await File.WriteAllTextAsync(Path.Combine(mainRepo, "file.txt"), "content");
            await RunGitAsync(mainRepo, "add file.txt");
            await RunGitAsync(mainRepo, "commit -m \"init\"");

            await RunGitAsync(mainRepo, $"worktree add {worktree} -b test-branch");

            return (mainRepo, worktree);
        }

        [Fact]
        public async Task DetectSourceRepoAsync_InWorktree_ReturnsMainRepoRoot()
        {
            (string mainRepo, string worktree) = await CreateGitRepoWithWorktreeAsync();

            string result = await _detector.DetectSourceRepoAsync(worktree, CancellationToken.None);

            Assert.Equal(
                Path.GetFullPath(mainRepo).TrimEnd(Path.DirectorySeparatorChar),
                result);
        }

        [Fact]
        public async Task DetectSourceRepoAsync_InMainRepo_ThrowsInvalidOperation()
        {
            string mainRepo = Path.Combine(_tempDir, "repo");
            Directory.CreateDirectory(mainRepo);

            await RunGitAsync(mainRepo, "init");
            await RunGitAsync(mainRepo, "config user.email test@test.com");
            await RunGitAsync(mainRepo, "config user.name Test");
            await File.WriteAllTextAsync(Path.Combine(mainRepo, "file.txt"), "content");
            await RunGitAsync(mainRepo, "add file.txt");
            await RunGitAsync(mainRepo, "commit -m \"init\"");

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _detector.DetectSourceRepoAsync(mainRepo, CancellationToken.None));

            Assert.Contains("not a worktree", ex.Message);
            Assert.Contains("wi init <source-path> <destination-path>", ex.Message);
        }

        [Fact]
        public async Task DetectSourceRepoAsync_NotAGitRepo_ThrowsInvalidOperation()
        {
            string notRepo = Path.Combine(_tempDir, "not_repo");
            Directory.CreateDirectory(notRepo);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _detector.DetectSourceRepoAsync(notRepo, CancellationToken.None));

            Assert.Contains("Not inside a git repository", ex.Message);
            Assert.Contains("wi init <source-path> <destination-path>", ex.Message);
        }

        [Fact]
        public async Task DetectSourceRepoAsync_NonExistentDir_ThrowsInvalidOperation()
        {
            string badPath = Path.Combine(_tempDir, "does_not_exist");

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _detector.DetectSourceRepoAsync(badPath, CancellationToken.None));

            Assert.Contains("wi init <source-path> <destination-path>", ex.Message);
        }
    }
}
