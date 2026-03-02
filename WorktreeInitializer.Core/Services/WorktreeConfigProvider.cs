using System.Text.Json;
using WorktreeInitializer.Core.Interfaces;

namespace WorktreeInitializer.Core.Services
{
    public class WorktreeConfigProvider : IWorktreeConfigProvider
    {
        private const string ConfigFileName = "WorktreeConfig.json";

        public async Task<List<string>> GetIgnorePatternsAsync(string repoPath, CancellationToken cancellationToken)
        {
            string configPath = Path.Combine(repoPath, ConfigFileName);

            if (!File.Exists(configPath))
            {
                return new List<string>();
            }

            string json = await File.ReadAllTextAsync(configPath, cancellationToken);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse {ConfigFileName}: {ex.Message}", ex);
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("ignores", out JsonElement ignoresElement))
                {
                    return new List<string>();
                }

                if (ignoresElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"Failed to parse {ConfigFileName}: 'ignores' must be an array.");
                }

                List<string> patterns = new List<string>();
                foreach (JsonElement item in ignoresElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidOperationException($"Failed to parse {ConfigFileName}: all entries in 'ignores' must be strings.");
                    }

                    string? value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        patterns.Add(value);
                    }
                }

                return patterns;
            }
        }
    }
}
