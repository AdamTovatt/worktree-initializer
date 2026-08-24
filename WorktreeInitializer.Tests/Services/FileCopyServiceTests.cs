using System.Runtime.Versioning;
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

        [Fact]
        public async Task CopyFileAsync_SymbolicLinkToFile_RecreatesLinkRatherThanContent()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "real.txt"), "linked content");
            string sourceLink = Path.Combine(sourceDir, "link.txt");
            File.CreateSymbolicLink(sourceLink, "real.txt");

            string destLink = Path.Combine(destDir, "link.txt");

            FileCopyResult result = await _service.CopyFileAsync("link.txt", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("real.txt", new FileInfo(destLink).LinkTarget);
        }

        [Fact]
        public async Task CopyFileAsync_RelativeSymbolicLink_ResolvesInsideDestinationTree()
        {
            // The failure this guards against is a link that keeps pointing at the source tree —
            // a workspace link copied that way makes the worktree read the original checkout.
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(Path.Combine(sourceDir, "nested"));
            Directory.CreateDirectory(Path.Combine(destDir, "nested"));

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "shared.txt"), "source copy");
            await File.WriteAllTextAsync(Path.Combine(destDir, "shared.txt"), "destination copy");

            string sourceLink = Path.Combine(sourceDir, "nested", "link.txt");
            File.CreateSymbolicLink(sourceLink, Path.Combine("..", "shared.txt"));

            string destLink = Path.Combine(destDir, "nested", "link.txt");

            FileCopyResult result = await _service.CopyFileAsync("nested/link.txt", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("destination copy", await File.ReadAllTextAsync(destLink));
        }

        [Fact]
        public async Task CopyFileAsync_SymbolicLinkToDirectory_RecreatesLink()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(Path.Combine(sourceDir, "target"));
            Directory.CreateDirectory(Path.Combine(destDir, "target"));

            await File.WriteAllTextAsync(Path.Combine(sourceDir, "target", "inner.txt"), "inner");
            await File.WriteAllTextAsync(Path.Combine(destDir, "target", "inner.txt"), "inner");

            string sourceLink = Path.Combine(sourceDir, "alias");
            File.CreateSymbolicLink(sourceLink, "target");

            string destLink = Path.Combine(destDir, "alias");

            FileCopyResult result = await _service.CopyFileAsync("alias", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("target", new DirectoryInfo(destLink).LinkTarget);
            Assert.True(File.Exists(Path.Combine(destLink, "inner.txt")));
        }

        [Fact]
        public async Task CopyFileAsync_BrokenSymbolicLink_IsRecreated()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceLink = Path.Combine(sourceDir, "dangling");
            File.CreateSymbolicLink(sourceLink, "not-here.txt");

            string destLink = Path.Combine(destDir, "dangling");

            FileCopyResult result = await _service.CopyFileAsync("dangling", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("not-here.txt", new FileInfo(destLink).LinkTarget);
        }

        [Fact]
        public async Task CopyFileAsync_ExistingLinkAtDestination_IsReplaced()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceLink = Path.Combine(sourceDir, "link");
            File.CreateSymbolicLink(sourceLink, "new-target.txt");

            string destLink = Path.Combine(destDir, "link");
            File.CreateSymbolicLink(destLink, "stale-target.txt");

            FileCopyResult result = await _service.CopyFileAsync("link", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("new-target.txt", new FileInfo(destLink).LinkTarget);
        }

        [Fact]
        public async Task CopyFileAsync_RegularFileOverExistingLink_ReplacesTheLink()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceFile = Path.Combine(sourceDir, "thing");
            await File.WriteAllTextAsync(sourceFile, "real content");

            await File.WriteAllTextAsync(Path.Combine(destDir, "elsewhere.txt"), "should not be written through");
            string destPath = Path.Combine(destDir, "thing");
            File.CreateSymbolicLink(destPath, "elsewhere.txt");

            FileCopyResult result = await _service.CopyFileAsync("thing", sourceFile, destPath, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("real content", await File.ReadAllTextAsync(destPath));
            Assert.Equal("should not be written through", await File.ReadAllTextAsync(Path.Combine(destDir, "elsewhere.txt")));
        }

        [UnixOnlyFact]
        [UnsupportedOSPlatform("windows")]
        public async Task CopyFileAsync_ExecutableFile_KeepsItsPermissionBits()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceFile = Path.Combine(sourceDir, "script.sh");
            await File.WriteAllTextAsync(sourceFile, "#!/bin/sh\necho hi\n");
            UnixFileMode executableMode =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(sourceFile, executableMode);

            string destFile = Path.Combine(destDir, "script.sh");

            FileCopyResult result = await _service.CopyFileAsync("script.sh", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(executableMode, File.GetUnixFileMode(destFile));
        }

        [UnixOnlyFact]
        [UnsupportedOSPlatform("windows")]
        public async Task CopyFileAsync_ReadOnlyFile_KeepsItsPermissionBits()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            string sourceFile = Path.Combine(sourceDir, "locked.txt");
            await File.WriteAllTextAsync(sourceFile, "content");
            UnixFileMode readOnlyMode = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            File.SetUnixFileMode(sourceFile, readOnlyMode);

            string destFile = Path.Combine(destDir, "locked.txt");

            FileCopyResult result = await _service.CopyFileAsync("locked.txt", sourceFile, destFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(readOnlyMode, File.GetUnixFileMode(destFile));
        }

        [UnixOnlyFact]
        [UnsupportedOSPlatform("windows")]
        public async Task CopyFileAsync_SymbolicLink_DoesNotChangeTheTargetsPermissionBits()
        {
            string sourceDir = Path.Combine(_tempDir, "source");
            string destDir = Path.Combine(_tempDir, "dest");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);

            // The source link resolves, and its target carries a different mode from the
            // destination's: applying the source's mode through the link would be visible here.
            string sourceTarget = Path.Combine(sourceDir, "real.txt");
            await File.WriteAllTextAsync(sourceTarget, "content");
            File.SetUnixFileMode(sourceTarget, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            string sourceLink = Path.Combine(sourceDir, "link.txt");
            File.CreateSymbolicLink(sourceLink, "real.txt");

            string destTarget = Path.Combine(destDir, "real.txt");
            await File.WriteAllTextAsync(destTarget, "content");
            UnixFileMode targetMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(destTarget, targetMode);

            string destLink = Path.Combine(destDir, "link.txt");

            FileCopyResult result = await _service.CopyFileAsync("link.txt", sourceLink, destLink, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(targetMode, File.GetUnixFileMode(destTarget));
        }
    }
}
