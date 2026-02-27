using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    /// <summary>
    /// Maps relative git paths (forward slashes) to full OS paths.
    /// </summary>
    public class PathMapper : IPathMapper
    {
        public string MapToFullPath(string relativePath, string rootPath)
        {
            string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(rootPath, normalizedRelative);
        }
    }
}
