using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Integration
{
    public class FullWorkflowTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly GitIgnoredFileProvider _gitProvider;
        private readonly PathMapper _pathMapper;
        private readonly FileCopyService _fileCopyService;

        public FullWorkflowTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_integration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _gitProvider = new GitIgnoredFileProvider();
            _pathMapper = new PathMapper();
            _fileCopyService = new FileCopyService();
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

        private async Task<string> CreateSourceRepo()
        {
            string repoDir = Path.Combine(_tempDir, "source");
            Directory.CreateDirectory(repoDir);

            await RunGitAsync(repoDir, "init");
            await RunGitAsync(repoDir, "config user.email test@test.com");
            await RunGitAsync(repoDir, "config user.name Test");

            // Create .gitignore with multiple patterns
            string gitignore = string.Join("\n", "*.log", "*.env", "bin/", "obj/");
            await File.WriteAllTextAsync(Path.Combine(repoDir, ".gitignore"), gitignore);

            // Create a tracked file
            await File.WriteAllTextAsync(Path.Combine(repoDir, "Program.cs"), "class Program {}");

            await RunGitAsync(repoDir, "add .gitignore Program.cs");
            await RunGitAsync(repoDir, "commit -m \"init\"");

            return repoDir;
        }

        [Fact]
        public async Task FullWorkflow_CopiesIgnoredFiles()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create ignored files in source
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "secrets.env"), "API_KEY=abc123");

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(destDir, "app.log")));
            Assert.True(File.Exists(Path.Combine(destDir, "secrets.env")));
            Assert.Equal("log data", await File.ReadAllTextAsync(Path.Combine(destDir, "app.log")));
            Assert.Equal("API_KEY=abc123", await File.ReadAllTextAsync(Path.Combine(destDir, "secrets.env")));
        }

        [Fact]
        public async Task FullWorkflow_PreservesDirectoryStructure()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create nested ignored files
            string binDir = Path.Combine(sourceDir, "bin", "Debug");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "binary content");

            string objDir = Path.Combine(sourceDir, "obj");
            Directory.CreateDirectory(objDir);
            await File.WriteAllTextAsync(Path.Combine(objDir, "project.assets.json"), "{}");

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(destDir, "bin", "Debug", "app.dll")));
            Assert.True(File.Exists(Path.Combine(destDir, "obj", "project.assets.json")));
        }

        [Fact]
        public async Task FullWorkflow_NoIgnoredFiles_SucceedsGracefully()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Don't create any ignored files — just the tracked ones from CreateSourceRepo

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("Nothing to copy", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task FullWorkflow_DoesNotCopyTrackedFiles()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create an ignored file
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "debug.log"), "some log");

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            // The tracked file Program.cs should NOT be copied
            Assert.False(File.Exists(Path.Combine(destDir, "Program.cs")));
            // The ignored file should be copied
            Assert.True(File.Exists(Path.Combine(destDir, "debug.log")));
        }
    }
}
