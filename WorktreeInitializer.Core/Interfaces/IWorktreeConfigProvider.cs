namespace WorktreeInitializer.Core.Interfaces
{
    public interface IWorktreeConfigProvider
    {
        Task<List<string>> GetIgnorePatternsAsync(string repoPath, CancellationToken cancellationToken);
    }
}
