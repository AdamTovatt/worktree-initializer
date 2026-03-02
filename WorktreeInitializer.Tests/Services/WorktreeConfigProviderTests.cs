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

        [Fact]
        public async Task GetIgnorePatternsAsync_NoConfigFile_ReturnsEmptyList()
        {
            List<string> result = await _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_ValidConfig_ReturnsPatterns()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"ignores": ["node_modules", ".venv", "dist"]}""");

            List<string> result = await _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None);

            Assert.Equal(3, result.Count);
            Assert.Contains("node_modules", result);
            Assert.Contains(".venv", result);
            Assert.Contains("dist", result);
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_EmptyIgnoresArray_ReturnsEmptyList()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"ignores": []}""");

            List<string> result = await _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_NoIgnoresProperty_ReturnsEmptyList()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"otherProp": true}""");

            List<string> result = await _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_MalformedJson_Throws()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                "not valid json {{{");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_IgnoresNotArray_Throws()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"ignores": "not an array"}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_IgnoresContainsNonString_Throws()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"ignores": ["valid", 123]}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None));
        }

        [Fact]
        public async Task GetIgnorePatternsAsync_WhitespaceEntries_AreSkipped()
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "WorktreeConfig.json"),
                """{"ignores": ["node_modules", "", "  ", "dist"]}""");

            List<string> result = await _provider.GetIgnorePatternsAsync(_tempDir, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains("node_modules", result);
            Assert.Contains("dist", result);
        }
    }
}
