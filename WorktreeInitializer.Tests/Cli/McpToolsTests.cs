using Moq;
using WorktreeInitializer.Cli;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Tests.Cli
{
    public class McpToolsTests
    {
        private readonly Mock<IGitIgnoredFileProvider> _mockGit;
        private readonly Mock<IPathMapper> _mockMapper;
        private readonly Mock<IFileCopyService> _mockCopy;
        private readonly Mock<IWorktreeConfigProvider> _mockConfig;
        private readonly Mock<IShellCommandRunner> _mockShell;
        private readonly Mock<IWorktreeDetector> _mockDetector;
        private readonly McpTools _mcpTools;

        public McpToolsTests()
        {
            _mockGit = new Mock<IGitIgnoredFileProvider>();
            _mockMapper = new Mock<IPathMapper>();
            _mockCopy = new Mock<IFileCopyService>();
            _mockConfig = new Mock<IWorktreeConfigProvider>();
            _mockShell = new Mock<IShellCommandRunner>();
            _mockDetector = new Mock<IWorktreeDetector>();

            _mockConfig
                .Setup(c => c.GetConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WorktreeConfig.Empty);

            _mcpTools = new McpTools(
                _mockGit.Object, _mockMapper.Object, _mockCopy.Object,
                _mockConfig.Object, _mockShell.Object, _mockDetector.Object);
        }

        [Fact]
        public async Task InitAsync_BothPathsProvided_DoesNotCallDetector()
        {
            _mockGit
                .Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            await _mcpTools.InitAsync(
                CancellationToken.None,
                sourcePath: "/source",
                destinationPath: "/dest");

            _mockDetector.Verify(
                d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task InitAsync_NoPathsProvided_CallsDetector()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/detected/source");
            _mockGit
                .Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            await _mcpTools.InitAsync(CancellationToken.None);

            _mockDetector.Verify(
                d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task InitAsync_NoPathsDetectionFails_ReturnsErrorMessage()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Not inside a git repository."));

            string result = await _mcpTools.InitAsync(CancellationToken.None);

            Assert.Contains("Auto-detection failed", result);
            Assert.Contains("Not inside a git repository", result);
        }

        [Fact]
        public async Task InitAsync_OnlySourceProvided_UsesCwdAsDestination()
        {
            _mockGit
                .Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            // sourcePath provided, destinationPath null — should use cwd, no detector call needed
            await _mcpTools.InitAsync(
                CancellationToken.None,
                sourcePath: "/source",
                destinationPath: null);

            _mockDetector.Verify(
                d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task InitAsync_OnlyDestinationProvided_CallsDetectorForSource()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/detected/source");
            _mockGit
                .Setup(g => g.GetIgnoredFilesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            await _mcpTools.InitAsync(
                CancellationToken.None,
                sourcePath: null,
                destinationPath: "/dest");

            _mockDetector.Verify(
                d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once());
        }
    }
}
