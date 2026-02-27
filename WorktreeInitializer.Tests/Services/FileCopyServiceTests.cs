using WorktreeInitializer.Core.Models;
using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class FileCopyServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FileCopyService _service = new FileCopyService();

        public FileCopyServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wi_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task CopyFileAsync_SuccessfulCopy_ReturnsSuccess()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceFile = Path.Combine(sourceDir, "test.txt");
            await File.WriteAllTextAsync(sourceFile, "hello world");

            string destFile = Path.Combine(destDir, "test.txt");

            FileCopyResult result = await _service.CopyFileAsync("test.txt", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(result.Error);
            Assert.True(File.Exists(destFile));
            Assert.Equal("hello world", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_CreatesDirectories_WhenNeeded()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            Directory.CreateDirectory(sourceDir);

            string sourceFile = Path.Combine(sourceDir, "test.txt");
            await File.WriteAllTextAsync(sourceFile, "content");

            string destFile = Path.Combine(_tempDir, "dest", "sub", "dir", "test.txt");

            FileCopyResult result = await _service.CopyFileAsync("sub/dir/test.txt", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(destFile));
            Assert.Equal("content", await File.ReadAllTextAsync(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_MissingSource_ReturnsFailure()
        {
            string sourceFile = Path.Combine(_tempDir, "nonexistent.txt");
            string destFile = Path.Combine(_tempDir, "dest.txt");

            FileCopyResult result = await _service.CopyFileAsync("nonexistent.txt", sourceFile, destFile, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task CopyFileAsync_BinaryFile_CopiedCorrectly()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            byte[] binaryData = new byte[1024];
            new Random(42).NextBytes(binaryData);

            string sourceFile = Path.Combine(sourceDir, "data.bin");
            await File.WriteAllBytesAsync(sourceFile, binaryData);

            string destFile = Path.Combine(destDir, "data.bin");

            FileCopyResult result = await _service.CopyFileAsync("data.bin", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            byte[] copiedData = await File.ReadAllBytesAsync(destFile);
            Assert.Equal(binaryData, copiedData);
        }

        [Fact]
        public async Task CopyFileAsync_OverwritesExistingFile()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceFile = Path.Combine(sourceDir, "test.txt");
            await File.WriteAllTextAsync(sourceFile, "new content");

            string destFile = Path.Combine(destDir, "test.txt");
            await File.WriteAllTextAsync(destFile, "old content");

            FileCopyResult result = await _service.CopyFileAsync("test.txt", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new content", await File.ReadAllTextAsync(destFile));
        }
    }
}
