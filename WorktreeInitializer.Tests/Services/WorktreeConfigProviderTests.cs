using WorktreeInitializer.Core.Models;
using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class WorktreeConfigProviderTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly WorktreeConfigProvider _provider;

        public WorktreeConfigProviderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_config_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _provider = new WorktreeConfigProvider();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private void WriteConfig(string json)
        {
            File.WriteAllText(Path.Combine(_tempDir, "WorktreeConfig.json"), json);
        }

        [Fact]
        public async Task GetConfigAsync_NoConfigFile_ReturnsEmptyConfig()
        {
            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result.Ignores);
            Assert.Empty(result.PostInitializeCommands);
        }

        [Fact]
        public async Task GetConfigAsync_ValidConfig_ReturnsPatterns()
        {
            WriteConfig("""{"ignores": ["node_modules", ".venv", "dist"]}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Equal(3, result.Ignores.Count);
            Assert.Contains("node_modules", result.Ignores);
            Assert.Contains(".venv", result.Ignores);
            Assert.Contains("dist", result.Ignores);
        }

        [Fact]
        public async Task GetConfigAsync_EmptyIgnoresArray_ReturnsEmptyList()
        {
            WriteConfig("""{"ignores": []}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result.Ignores);
        }

        [Fact]
        public async Task GetConfigAsync_NoIgnoresProperty_ReturnsEmptyList()
        {
            WriteConfig("""{"otherProp": true}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result.Ignores);
        }

        [Fact]
        public async Task GetConfigAsync_MalformedJson_Throws()
        {
            WriteConfig("not valid json {{{");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetConfigAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_IgnoresNotArray_Throws()
        {
            WriteConfig("""{"ignores": "not an array"}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetConfigAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_IgnoresContainsNonString_Throws()
        {
            WriteConfig("""{"ignores": ["valid", 123]}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetConfigAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_WhitespaceEntries_AreSkipped()
        {
            WriteConfig("""{"ignores": ["node_modules", "", "  ", "dist"]}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Equal(2, result.Ignores.Count);
            Assert.Contains("node_modules", result.Ignores);
            Assert.Contains("dist", result.Ignores);
        }

        [Fact]
        public async Task GetConfigAsync_PostInitialize_ReturnsCommandsInOrder()
        {
            WriteConfig("""{"postInitialize": ["npm rebuild", "npm install"]}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Equal(new[] { "npm rebuild", "npm install" }, result.PostInitializeCommands);
        }

        [Fact]
        public async Task GetConfigAsync_NoPostInitializeProperty_ReturnsEmptyList()
        {
            WriteConfig("""{"ignores": ["dist"]}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result.PostInitializeCommands);
        }

        [Fact]
        public async Task GetConfigAsync_PostInitializeNotArray_Throws()
        {
            WriteConfig("""{"postInitialize": "npm install"}""");

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetConfigAsync(_tempDir, CancellationToken.None));

            Assert.Contains("postInitialize", exception.Message);
        }

        [Fact]
        public async Task GetConfigAsync_PostInitializeContainsNonString_Throws()
        {
            WriteConfig("""{"postInitialize": ["npm install", 7]}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetConfigAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetConfigAsync_BothProperties_ReturnsBoth()
        {
            WriteConfig("""{"ignores": ["node_modules"], "postInitialize": ["npm install"]}""");

            WorktreeConfig result = await _provider.GetConfigAsync(_tempDir, CancellationToken.None);

            Assert.Equal(new[] { "node_modules" }, result.Ignores);
            Assert.Equal(new[] { "npm install" }, result.PostInitializeCommands);
        }
    }
}
