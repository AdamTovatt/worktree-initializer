using WorktreeInitializer.Core.Commands;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Tests.Commands
{
    public class HelpCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_ReturnsHelpText()
        {
            HelpCommand command = new HelpCommand();

            CommandResult result = await command.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("wi", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Details);
            Assert.Contains("init", result.Details);
            Assert.Contains("source-path", result.Details);
            Assert.Contains("destination-path", result.Details);
            Assert.Contains("--mcp", result.Details);
        }
    }
}
