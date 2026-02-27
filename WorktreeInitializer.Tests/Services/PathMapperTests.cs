using WorktreeInitializer.Core.Services;

namespace WorktreeInitializer.Tests.Services
{
    public class PathMapperTests
    {
        private readonly PathMapper _mapper = new PathMapper();

        [Fact]
        public void MapToFullPath_SimpleFile_CombinesWithRoot()
        {
            string result = _mapper.MapToFullPath("file.txt", "/root");
            Assert.Equal(Path.Combine("/root", "file.txt"), result);
        }

        [Fact]
        public void MapToFullPath_ForwardSlashes_ConvertedToOsSeparator()
        {
            string result = _mapper.MapToFullPath("dir/file.txt", "/root");
            string expected = Path.Combine("/root", "dir", "file.txt");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MapToFullPath_NestedPath_PreservesStructure()
        {
            string result = _mapper.MapToFullPath("a/b/c/file.txt", "/root");
            string expected = Path.Combine("/root", "a", "b", "c", "file.txt");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MapToFullPath_DifferentRoots_ProducesDifferentPaths()
        {
            string source = _mapper.MapToFullPath("dir/file.txt", "/source");
            string dest = _mapper.MapToFullPath("dir/file.txt", "/dest");
            Assert.NotEqual(source, dest);
            Assert.EndsWith(Path.Combine("dir", "file.txt"), source);
            Assert.EndsWith(Path.Combine("dir", "file.txt"), dest);
        }

        [Fact]
        public void MapToFullPath_PathWithSpaces_HandledCorrectly()
        {
            string result = _mapper.MapToFullPath("my dir/my file.txt", "/root path");
            string expected = Path.Combine("/root path", "my dir", "my file.txt");
            Assert.Equal(expected, result);
        }
    }
}
