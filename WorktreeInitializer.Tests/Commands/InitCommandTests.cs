using Moq;
using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Tests.Commands
{
    public class InitCommandTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Mock<IGitIgnoredFileProvider> _mockGit;
        private readonly Mock<IPathMapper> _mockMapper;
        private readonly Mock<IFileCopyService> _mockCopy;
        private readonly Mock<IWorktreeConfigProvider> _mockConfig;
        private readonly Mock<IShellCommandRunner> _mockShell;

        public InitCommandTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _mockGit = new Mock<IGitIgnoredFileProvider>();
            _mockMapper = new Mock<IPathMapper>();
            _mockCopy = new Mock<IFileCopyService>();
            _mockConfig = new Mock<IWorktreeConfigProvider>();
            _mockShell = new Mock<IShellCommandRunner>();

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WorktreeConfig.Empty);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private (string source, string dest) CreateSourceAndDest()
        {
            string source = Path.Combine(_tempDir, "source");
            string dest = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(dest);
            return (source, dest);
        }

        [Fact]
        public async Task ExecuteAsync_WithFilesToCopy_ReturnsSuccessWithCount()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "a.log", "b.tmp", "dir/c.env" });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("3", result.Message);
            _mockCopy.Verify(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_NoIgnoredFiles_ReturnsNothingToCopy()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("Nothing to copy", result.Message, StringComparison.OrdinalIgnoreCase);
            _mockCopy.Verify(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_SourceNotFound_ReturnsFailure()
        {
            string dest = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(dest);
            string nonExistentSource = Path.Combine(_tempDir, "no_such_source");

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, nonExistentSource, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_DestinationNotFound_ReturnsFailure()
        {
            string source = Path.Combine(_tempDir, "source");
            Directory.CreateDirectory(source);
            string nonExistentDest = Path.Combine(_tempDir, "no_such_dest");

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, nonExistentDest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_GitFails_ReturnsFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("git is not installed"));

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("git is not installed", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_SomeFilesFail_ReportsPartialFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "good.log", "bad.log", "ok.log" });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync("good.log", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileCopyResult("good.log", "s", "d", Success: true));
            _mockCopy.Setup(c => c.CopyFileAsync("bad.log", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileCopyResult("bad.log", "s", "d", Success: false, Error: "Access denied"));
            _mockCopy.Setup(c => c.CopyFileAsync("ok.log", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileCopyResult("ok.log", "s", "d", Success: true));

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("2", result.Message);
            Assert.Contains("1 failed", result.Message);
            Assert.NotNull(result.Details);
            Assert.Contains("bad.log", result.Details);
            Assert.Contains("Access denied", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_AllFilesFail_ReportsAllFailures()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "a.log", "b.log" });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: false, Error: "Permission denied"));

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("0", result.Message);
            Assert.Contains("2 failed", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_WithCliIgnorePaths_SkipsMatchingFiles()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>
                {
                    "node_modules/lodash/index.js",
                    "node_modules/express/index.js",
                    "src/app.env",
                    ".venv/bin/python"
                });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                ignorePaths: new List<string> { "node_modules", ".venv" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("1", result.Message); // only src/app.env
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync(It.Is<string>(s => s.StartsWith("node_modules")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithConfigIgnores_SkipsMatchingFiles()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "dist/bundle.js", "src/app.env" });

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorktreeConfig(new List<string> { "dist" }, Array.Empty<string>()));

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync("dist/bundle.js", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_CliAndConfigIgnoresMerged()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>
                {
                    "node_modules/pkg/index.js",
                    "dist/bundle.js",
                    "src/app.env"
                });

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorktreeConfig(new List<string> { "dist" }, Array.Empty<string>()));

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                ignorePaths: new List<string> { "node_modules" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("1", result.Message); // only src/app.env
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ExactPathMatch_IsIgnored()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "node_modules", "src/app.env" });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                ignorePaths: new List<string> { "node_modules" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync("node_modules", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_IncludeOverridesCliIgnore_FileIsCopied()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>
                {
                    "node_modules/lodash/index.js",
                    "src/app.env"
                });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                ignorePaths: new List<string> { "node_modules" },
                includePaths: new List<string> { "node_modules" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("2", result.Message);
            _mockCopy.Verify(c => c.CopyFileAsync("node_modules/lodash/index.js", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_IncludeOverridesConfigIgnore_FileIsCopied()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "dist/bundle.js", "src/app.env" });

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorktreeConfig(new List<string> { "dist" }, Array.Empty<string>()));

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                includePaths: new List<string> { "dist" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("2", result.Message);
            _mockCopy.Verify(c => c.CopyFileAsync("dist/bundle.js", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_IncludeAndIgnoreSamePattern_FileIsCopied()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>
                {
                    "node_modules/pkg/index.js",
                    ".venv/bin/python",
                    "src/app.env"
                });

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorktreeConfig(new List<string> { "node_modules" }, Array.Empty<string>()));

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string rel, string src, string dst, CancellationToken _) =>
                    new FileCopyResult(rel, src, dst, Success: true));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest,
                ignorePaths: new List<string> { ".venv", "node_modules" },
                includePaths: new List<string> { "node_modules" });

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            // node_modules is included (overrides both CLI and config ignore), .venv is still ignored
            Assert.Contains("2", result.Message);
            _mockCopy.Verify(c => c.CopyFileAsync("node_modules/pkg/index.js", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync("src/app.env", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockCopy.Verify(c => c.CopyFileAsync(It.Is<string>(s => s.StartsWith(".venv")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ConfigReadFails_ReturnsFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "a.log" });

            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Failed to parse WorktreeConfig.json: invalid JSON"));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Failed to read config", result.Message);
        }

        private void GivenPostInitializeCommands(params string[] commands)
        {
            _mockConfig.Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorktreeConfig(Array.Empty<string>(), commands));
        }

        private void GivenCommandExits(string command, int exitCode, string output = "")
        {
            _mockShell.Setup(s => s.RunAsync(It.IsAny<string>(), command, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShellCommandResult(command, exitCode, output));
        }

        [Fact]
        public async Task ExecuteAsync_PostInitializeCommands_RunInDestinationInOrder()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            GivenPostInitializeCommands("npm rebuild", "npm install");

            List<string> executionOrder = new List<string>();
            _mockShell.Setup(s => s.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string _, string command, CancellationToken _) => executionOrder.Add(command))
                .ReturnsAsync((string _, string command, CancellationToken _) => new ShellCommandResult(command, 0, ""));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(new[] { "npm rebuild", "npm install" }, executionOrder);
            _mockShell.Verify(s => s.RunAsync(Path.GetFullPath(dest), "npm install", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_PostInitializeCommandFails_StopsAndReportsFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            GivenPostInitializeCommands("npm install", "npm run build");
            GivenCommandExits("npm install", 1, "npm ERR! could not resolve");
            GivenCommandExits("npm run build", 0);

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("npm install", result.Message);
            Assert.NotNull(result.Details);
            Assert.Contains("exit code 1", result.Details);
            Assert.Contains("npm ERR! could not resolve", result.Details);
            _mockShell.Verify(s => s.RunAsync(It.IsAny<string>(), "npm run build", It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_PostInitializeRunnerThrows_ReportsFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            GivenPostInitializeCommands("npm install");
            _mockShell.Setup(s => s.RunAsync(It.IsAny<string>(), "npm install", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Command timed out after 30 minutes: npm install"));

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Details);
            Assert.Contains("timed out", result.Details);
        }

        [Fact]
        public async Task ExecuteAsync_NothingToCopy_StillRunsPostInitializeCommands()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            GivenPostInitializeCommands("npm install");
            GivenCommandExits("npm install", 0);

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("Nothing to copy", result.Message, StringComparison.OrdinalIgnoreCase);
            _mockShell.Verify(s => s.RunAsync(It.IsAny<string>(), "npm install", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoPostInitializeCommands_RunsNothing()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            _mockShell.Verify(s => s.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_CopyFailedAndPostInitializeSucceeded_StillReportsFailure()
        {
            (string source, string dest) = CreateSourceAndDest();

            _mockGit.Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "a.log" });

            _mockMapper.Setup(m => m.MapToFullPath(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((rel, root) => Path.Combine(root, rel));

            _mockCopy.Setup(c => c.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FileCopyResult("a.log", "s", "d", Success: false, Error: "Access denied"));

            GivenPostInitializeCommands("npm install");
            GivenCommandExits("npm install", 0);

            InitCommand command = new InitCommand(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object, _mockConfig.Object, _mockShell.Object,
                source, dest);

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Details);
            Assert.Contains("a.log", result.Details);
        }
    }
}
