using System.Text.Json;
using WorktreeInitializer.Core.Interfaces;
using WorktreeInitializer.Core.Models;

namespace WorktreeInitializer.Core.Services
{
    public class WorktreeConfigProvider : IWorktreeConfigProvider
    {
        private const string ConfigFileName = "WorktreeConfig.json";
        private const string IgnoresPropertyName = "ignores";
        private const string PostInitializePropertyName = "postInitialize";

        public async Task<WorktreeConfig> GetConfigAsync(string repoPath, CancellationToken cancellationToken)
        {
            string configPath = Path.Combine(repoPath, ConfigFileName);

            if (!File.Exists(configPath))
            {
                return WorktreeConfig.Empty;
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
                List<string> ignores = ReadStringArray(doc.RootElement, IgnoresPropertyName);
                List<string> postInitialize = ReadStringArray(doc.RootElement, PostInitializePropertyName);

                return new WorktreeConfig(ignores, postInitialize);
            }
        }

        private static List<string> ReadStringArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element))
            {
                return new List<string>();
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Failed to parse {ConfigFileName}: '{propertyName}' must be an array.");
            }

            List<string> values = new List<string>();
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"Failed to parse {ConfigFileName}: all entries in '{propertyName}' must be strings.");
                }

                string? value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }
    }
}
