using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class GitIgnoredFileProviderTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly GitIgnoredFileProvider _provider = new GitIgnoredFileProvider();

        public GitIgnoredFileProviderTests()
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

        private async Task<string> CreateGitRepoWithIgnoredFiles(params (string path, string content)[] ignoredFiles)
        {
            string repoDir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoDir);

            await RunGitAsync(repoDir, "init");
            await RunGitAsync(repoDir, "config user.email test@test.com");
            await RunGitAsync(repoDir, "config user.name Test");

            // Create .gitignore
            string patterns = string.Join(Environment.NewLine, ignoredFiles.Select(f => f.path));
            await File.WriteAllTextAsync(Path.Combine(repoDir, ".gitignore"), patterns);

            // Create the ignored files
            foreach ((string path, string content) in ignoredFiles)
            {
                string fullPath = Path.Combine(repoDir, path.Replace('/', Path.DirectorySeparatorChar));
                string? dir = Path.GetDirectoryName(fullPath);
                if (dir != null) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(fullPath, content);
            }

            // Stage and commit .gitignore so git recognizes it
            await RunGitAsync(repoDir, "add .gitignore");
            await RunGitAsync(repoDir, "commit -m \"init\"");

            return repoDir;
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_ReturnsIgnoredFiles()
        {
            string repoDir = await CreateGitRepoWithIgnoredFiles(("secret.env", "SECRET=123"));

            List<string> files = await _provider.GetIgnoredFilesAsync(repoDir, CancellationToken.None);

            Assert.Single(files);
            Assert.Equal("secret.env", files[0]);
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_EmptyRepo_ReturnsEmptyList()
        {
            string repoDir = Path.Combine(_tempDir, "empty");
            Directory.CreateDirectory(repoDir);
            await RunGitAsync(repoDir, "init");
            await RunGitAsync(repoDir, "config user.email test@test.com");
            await RunGitAsync(repoDir, "config user.name Test");

            List<string> files = await _provider.GetIgnoredFilesAsync(repoDir, CancellationToken.None);

            Assert.Empty(files);
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_NonExistentDir_ThrowsInvalidOperation()
        {
            string badPath = Path.Combine(_tempDir, "does_not_exist");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetIgnoredFilesAsync(badPath, CancellationToken.None));
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_NotAGitRepo_ThrowsInvalidOperation()
        {
            string notRepo = Path.Combine(_tempDir, "not_repo");
            Directory.CreateDirectory(notRepo);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetIgnoredFilesAsync(notRepo, CancellationToken.None));
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_NestedIgnoredFiles_ReturnsRelativePaths()
        {
            string repoDir = await CreateGitRepoWithIgnoredFiles(("build/output.dll", "binary"));

            List<string> files = await _provider.GetIgnoredFilesAsync(repoDir, CancellationToken.None);

            Assert.Single(files);
            Assert.Equal("build/output.dll", files[0]);
        }

        [Fact]
        public async Task GetIgnoredFilesAsync_MultiplePatterns_ReturnsAllIgnoredFiles()
        {
            string repoDir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repoDir);

            await RunGitAsync(repoDir, "init");
            await RunGitAsync(repoDir, "config user.email test@test.com");
            await RunGitAsync(repoDir, "config user.name Test");

            await File.WriteAllTextAsync(Path.Combine(repoDir, ".gitignore"), "*.log\n*.tmp\n");
            await File.WriteAllTextAsync(Path.Combine(repoDir, "app.log"), "log data");
            await File.WriteAllTextAsync(Path.Combine(repoDir, "cache.tmp"), "tmp data");
            await File.WriteAllTextAsync(Path.Combine(repoDir, "keep.txt"), "keep this");

            await RunGitAsync(repoDir, "add .gitignore keep.txt");
            await RunGitAsync(repoDir, "commit -m \"init\"");

            List<string> files = await _provider.GetIgnoredFilesAsync(repoDir, CancellationToken.None);

            Assert.Equal(2, files.Count);
            Assert.Contains("app.log", files);
            Assert.Contains("cache.tmp", files);
        }
    }

    public class GitIgnoredFileProviderParseOutputTests
    {
        [Fact]
        public void ParseOutput_SimplePaths_ReturnsPaths()
        {
            string output = "src/file.cs\0build/output.dll\0";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Equal(2, result.Count);
            Assert.Equal("src/file.cs", result[0]);
            Assert.Equal("build/output.dll", result[1]);
        }

        [Fact]
        public void ParseOutput_EmptyOutput_ReturnsEmptyList()
        {
            string output = "";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Empty(result);
        }

        [Fact]
        public void ParseOutput_BackslashesInPaths_NormalizesToForwardSlashes()
        {
            string output = "obj\\Debug\\file.props\0";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Single(result);
            Assert.Equal("obj/Debug/file.props", result[0]);
        }

        [Fact]
        public void ParseOutput_DuplicatePathsAfterNormalization_Deduplicates()
        {
            string output = "obj/Debug/file.props\0obj\\Debug\\file.props\0";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Single(result);
            Assert.Equal("obj/Debug/file.props", result[0]);
        }

        [Fact]
        public void ParseOutput_MixedSeparators_NormalizesAll()
        {
            string output = "obj\\Debug/\\package.g.props\0obj/Debug/package.g.props\0";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Single(result);
            Assert.Equal("obj/Debug/package.g.props", result[0]);
        }

        [Fact]
        public void ParseOutput_TrailingNullByte_DoesNotCreateEmptyEntry()
        {
            string output = "file.txt\0";

            List<string> result = GitIgnoredFileProvider.ParseOutput(output);

            Assert.Single(result);
            Assert.Equal("file.txt", result[0]);
        }
    }
}
