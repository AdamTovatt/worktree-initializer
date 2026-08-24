namespace WorktreeInitializer.Core.Models
{
    /// <summary>
    /// The settings a source repository declares in its WorktreeConfig.json.
    /// </summary>
    /// <param name="Ignores">Path prefixes excluded from the copy, merged with any --ignore flags.</param>
    /// <param name="PostInitializeCommands">Shell commands run in the destination worktree once copying has finished, in order.</param>
    public record WorktreeConfig(IReadOnlyList<string> Ignores, IReadOnlyList<string> PostInitializeCommands)
    {
        /// <summary>
        /// The configuration of a repository that declares none — no ignores, no commands.
        /// </summary>
        public static WorktreeConfig Empty { get; } = new WorktreeConfig(Array.Empty<string>(), Array.Empty<string>());
    }
}
