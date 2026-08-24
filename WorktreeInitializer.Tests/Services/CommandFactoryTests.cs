using Moq;
using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class CommandFactoryTests
    {
        private readonly Mock<IWorktreeDetector> _mockDetector;
        private readonly CommandFactory _factory;

        public CommandFactoryTests()
        {
            Mock<IGitIgnoredFileProvider> mockGit = new Mock<IGitIgnoredFileProvider>();
            Mock<IPathMapper> mockMapper = new Mock<IPathMapper>();
            Mock<IFileCopyService> mockCopy = new Mock<IFileCopyService>();
            Mock<IWorktreeConfigProvider> mockConfig = new Mock<IWorktreeConfigProvider>();
            Mock<IShellCommandRunner> mockShell = new Mock<IShellCommandRunner>();
            _mockDetector = new Mock<IWorktreeDetector>();
            _factory = new CommandFactory(mockGit.Object, mockMapper.Object, mockCopy.Object, mockConfig.Object, mockShell.Object, _mockDetector.Object);
        }

        [Fact]
        public void CreateCommand_NoArgs_ReturnsHelpCommand()
        {
            ICommand command = _factory.CreateCommand(Array.Empty<string>());
            Assert.IsType<HelpCommand>(command);
        }

        [Fact]
        public void CreateCommand_Help_ReturnsHelpCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "help" });
            Assert.IsType<HelpCommand>(command);
        }

        [Fact]
        public void CreateCommand_DashDashHelp_ReturnsHelpCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "--help" });
            Assert.IsType<HelpCommand>(command);
        }

        [Fact]
        public void CreateCommand_DashH_ReturnsHelpCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "-h" });
            Assert.IsType<HelpCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_WithPaths_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest" });
            Assert.IsType<InitCommand>(command);
            _mockDetector.Verify(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        [Fact]
        public void CreateCommand_Init_CaseInsensitive_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "INIT", "/source", "/dest" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_NoPaths_AutoDetectSucceeds_ReturnsInitCommand()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/detected/source");

            ICommand command = _factory.CreateCommand(new[] { "init" });
            Assert.IsType<InitCommand>(command);
            _mockDetector.Verify(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public void CreateCommand_Init_NoPaths_AutoDetectFails_ThrowsArgumentException()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException(
                    "Not inside a git repository. Use explicit paths: wi init <source-path> <destination-path>"));

            ArgumentException ex = Assert.Throws<ArgumentException>(() => _factory.CreateCommand(new[] { "init" }));
            Assert.Contains("wi init <source-path> <destination-path>", ex.Message);
        }

        [Fact]
        public void CreateCommand_Init_NoPathsWithFlags_AutoDetectSucceeds_ReturnsInitCommand()
        {
            _mockDetector
                .Setup(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("/detected/source");

            ICommand command = _factory.CreateCommand(new[] { "init", "--ignore", "node_modules" });
            Assert.IsType<InitCommand>(command);
            _mockDetector.Verify(d => d.DetectSourceRepoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public void CreateCommand_Init_OnePositionalArg_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _factory.CreateCommand(new[] { "init", "/source" }));
        }

        [Fact]
        public void CreateCommand_UnknownCommand_ThrowsArgumentException()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => _factory.CreateCommand(new[] { "unknown" }));
            Assert.Contains("unknown", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CreateCommand_Init_WithIgnoreFlags_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--ignore", "node_modules" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_WithMultipleIgnoreFlags_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--ignore", "node_modules", "--ignore", ".venv" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_IgnoreFlagWithoutValue_IsSkipped()
        {
            // --ignore at end with no value should not crash
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--ignore" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_WithIncludeFlag_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--include", "node_modules" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_WithMultipleIncludeFlags_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--include", "node_modules", "--include", "dist" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_WithIgnoreAndIncludeFlags_ReturnsInitCommand()
        {
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--ignore", "node_modules", "--include", "node_modules" });
            Assert.IsType<InitCommand>(command);
        }

        [Fact]
        public void CreateCommand_Init_IncludeFlagWithoutValue_IsSkipped()
        {
            // --include at end with no value should not crash
            ICommand command = _factory.CreateCommand(new[] { "init", "/source", "/dest", "--include" });
            Assert.IsType<InitCommand>(command);
        }
    }
}
