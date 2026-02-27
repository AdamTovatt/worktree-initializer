namespace WorktreeInitializer.Core.Interfaces
{
    /// <summary>
    /// Maps relative git paths to full OS paths.
    /// </summary>
    public interface IPathMapper
    {
        /// <summary>
        /// Combines a relative git path with a root directory to produce a full OS path.
        /// </summary>
        /// <param name="relativePath">The relative path from git (using forward slashes).</param>
        /// <param name="rootPath">The root directory to combine with.</param>
        /// <returns>The full OS path.</returns>
        string MapToFullPath(string relativePath, string rootPath);
    }
}
