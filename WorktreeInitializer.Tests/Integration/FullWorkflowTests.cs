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
        private readonly WorktreeConfigProvider _configProvider;
        private readonly ShellCommandRunner _shellRunner;
        private readonly WorktreeDetector _worktreeDetector;

        public FullWorkflowTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_integration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            GitProcessRunner gitRunner = new GitProcessRunner();
            _gitProvider = new GitIgnoredFileProvider(gitRunner);
            _pathMapper = new PathMapper();
            _fileCopyService = new FileCopyService();
            _configProvider = new WorktreeConfigProvider();
            _shellRunner = new ShellCommandRunner();
            _worktreeDetector = new WorktreeDetector(gitRunner);
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

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
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

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
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

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
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

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            // The tracked file Program.cs should NOT be copied
            Assert.False(File.Exists(Path.Combine(destDir, "Program.cs")));
            // The ignored file should be copied
            Assert.True(File.Exists(Path.Combine(destDir, "debug.log")));
        }

        [Fact]
        public async Task FullWorkflow_CliIgnore_SkipsMatchingFiles()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create ignored files: bin/ and obj/ are in .gitignore, plus a .log file
            string binDir = Path.Combine(sourceDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "binary");

            string objDir = Path.Combine(sourceDir, "obj");
            Directory.CreateDirectory(objDir);
            await File.WriteAllTextAsync(Path.Combine(objDir, "cache.json"), "{}");

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");

            // Use --ignore to skip bin and obj
            InitCommand command = new InitCommand(
                _gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner,
                sourceDir, destDir,
                ignorePaths: new List<string> { "bin", "obj" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            // app.log should be copied
            Assert.True(File.Exists(Path.Combine(destDir, "app.log")));
            // bin/ and obj/ should be skipped
            Assert.False(File.Exists(Path.Combine(destDir, "bin", "app.dll")));
            Assert.False(File.Exists(Path.Combine(destDir, "obj", "cache.json")));
        }

        [Fact]
        public async Task FullWorkflow_WorktreeConfigJson_SkipsMatchingFiles()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create ignored files
            string binDir = Path.Combine(sourceDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "binary");

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");

            // Place a WorktreeConfig.json that ignores bin
            await File.WriteAllTextAsync(
                Path.Combine(sourceDir, "WorktreeConfig.json"),
                """{"ignores": ["bin"]}""");

            InitCommand command = new InitCommand(
                _gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner,
                sourceDir, destDir);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(destDir, "app.log")));
            Assert.False(File.Exists(Path.Combine(destDir, "bin", "app.dll")));
        }

        [Fact]
        public async Task FullWorkflow_CliAndConfigIgnoresMerged()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create ignored files under bin/, obj/, and a .log
            string binDir = Path.Combine(sourceDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "binary");

            string objDir = Path.Combine(sourceDir, "obj");
            Directory.CreateDirectory(objDir);
            await File.WriteAllTextAsync(Path.Combine(objDir, "cache.json"), "{}");

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");

            // Config ignores bin, CLI ignores obj — both should be skipped
            await File.WriteAllTextAsync(
                Path.Combine(sourceDir, "WorktreeConfig.json"),
                """{"ignores": ["bin"]}""");

            InitCommand command = new InitCommand(
                _gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner,
                sourceDir, destDir,
                ignorePaths: new List<string> { "obj" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(destDir, "app.log")));
            Assert.False(File.Exists(Path.Combine(destDir, "bin", "app.dll")));
            Assert.False(File.Exists(Path.Combine(destDir, "obj", "cache.json")));
        }
        [Fact]
        public async Task FullWorkflow_ConfigIgnoreWithCliInclude_IncludeWins()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Create ignored files under bin/ and a .log
            string binDir = Path.Combine(sourceDir, "bin");
            Directory.CreateDirectory(binDir);
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "binary");

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");

            // Config ignores bin
            await File.WriteAllTextAsync(
                Path.Combine(sourceDir, "WorktreeConfig.json"),
                """{"ignores": ["bin"]}""");

            // CLI --include re-includes bin
            InitCommand command = new InitCommand(
                _gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner,
                sourceDir, destDir,
                includePaths: new List<string> { "bin" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            // bin should be copied because include wins over config ignore
            Assert.True(File.Exists(Path.Combine(destDir, "bin", "app.dll")));
            Assert.True(File.Exists(Path.Combine(destDir, "app.log")));
        }

        [Fact]
        public async Task FullWorkflow_AutoDetect_InWorktree_CopiesIgnoredFiles()
        {
            string sourceDir = await CreateSourceRepo();

            // Create ignored files in source
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "secrets.env"), "API_KEY=abc123");

            // Create a worktree from the source repo
            string worktreeDir = Path.Combine(_tempDir, "worktree");
            await RunGitAsync(sourceDir, $"worktree add {worktreeDir} -b test-branch");

            // Go through CommandFactory with auto-detect (no paths)
            // Inject the worktree path as the working directory instead of mutating global state
            CommandFactory factory = new CommandFactory(
                _gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, _worktreeDetector,
                getWorkingDirectory: () => worktreeDir);

            ICommand command = factory.CreateCommand(new[] { "init" });
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(worktreeDir, "app.log")));
            Assert.True(File.Exists(Path.Combine(worktreeDir, "secrets.env")));
            Assert.Equal("log data", await File.ReadAllTextAsync(Path.Combine(worktreeDir, "app.log")));
            Assert.Equal("API_KEY=abc123", await File.ReadAllTextAsync(Path.Combine(worktreeDir, "secrets.env")));
        }

        [Fact]
        public async Task FullWorkflow_IgnoredSymbolicLink_ArrivesAsALinkPointingInsideTheWorktree()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            // Both trees carry their own copy of the link's target, so a link copied by
            // dereferencing, or rewritten to an absolute path, reads the source's copy. The
            // target itself is not gitignored, so the copy never replaces the destination's.
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "target.txt"), "SOURCE");
            await File.WriteAllTextAsync(Path.Combine(destDir, "target.txt"), "DESTINATION");

            File.CreateSymbolicLink(Path.Combine(sourceDir, "alias.env"), "target.txt");

            InitCommand command = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);

            string copiedLink = Path.Combine(destDir, "alias.env");
            Assert.Equal("target.txt", new FileInfo(copiedLink).LinkTarget);
            Assert.Equal("DESTINATION", await File.ReadAllTextAsync(copiedLink));
        }

        [Fact]
        public async Task FullWorkflow_PostInitializeCommand_RunsInTheDestination()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "app.log"), "log data");

            await File.WriteAllTextAsync(
                Path.Combine(sourceDir, "WorktreeConfig.json"),
                """{"postInitialize": ["echo ran > marker.txt"]}""");

            InitCommand initCommand = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
            CommandResult result = await initCommand.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(destDir, "marker.txt")));
            Assert.False(File.Exists(Path.Combine(sourceDir, "marker.txt")));
        }

        [Fact]
        public async Task FullWorkflow_FailingPostInitializeCommand_FailsTheInit()
        {
            string sourceDir = await CreateSourceRepo();
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(destDir);

            await File.WriteAllTextAsync(
                Path.Combine(sourceDir, "WorktreeConfig.json"),
                """{"postInitialize": ["exit 3", "echo should-not-run > marker.txt"]}""");

            InitCommand initCommand = new InitCommand(_gitProvider, _pathMapper, _fileCopyService, _configProvider, _shellRunner, sourceDir, destDir);
            CommandResult result = await initCommand.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("exit 3", result.Message);
            Assert.NotNull(result.Details);
            Assert.Contains("exit code 3", result.Details);
            Assert.False(File.Exists(Path.Combine(destDir, "marker.txt")));
        }
    }
}
