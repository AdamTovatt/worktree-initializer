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

        public InitCommandTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _mockGit = new Mock<IGitIgnoredFileProvider>();
            _mockMapper = new Mock<IPathMapper>();
            _mockCopy = new Mock<IFileCopyService>();
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, dest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, dest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, nonExistentSource, dest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, nonExistentDest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, dest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, dest);
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

            InitCommand command = new InitCommand(_mockGit.Object, _mockMapper.Object, _mockCopy.Object, source, dest);
            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("0", result.Message);
            Assert.Contains("2 failed", result.Message);
        }
    }
}
