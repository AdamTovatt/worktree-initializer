namespace WorktreeInitializer.Tests
{
    /// <summary>
    /// A fact that only runs on unix-like platforms. It reports as skipped elsewhere rather than
    /// passing, so a run on Windows cannot be mistaken for evidence about permission bits.
    /// </summary>
    public sealed class UnixOnlyFactAttribute : FactAttribute
    {
        public UnixOnlyFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                Skip = "Unix file permissions are not a Windows concept.";
            }
        }
    }
}
