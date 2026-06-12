using System.Text.Json;

namespace PapersCli.Cli.Commands;

internal static class SearchResultInputParser
{
    public static IReadOnlyList<string> ParseIds(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(input);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => ParseResultArray(doc.RootElement),
                JsonValueKind.Object => ParseResultsPage(doc.RootElement),
                _ => ParseLines(input),
            };
        }
        catch (JsonException)
        {
            return ParseLines(input);
        }
    }

    private static IReadOnlyList<string> ParseResultArray(JsonElement results)
    {
        var ids = new List<string>();
        foreach (var item in results.EnumerateArray())
        {
            var id = ParseResultId(item);
            if (id is not null)
                ids.Add(id);
        }
        return ids;
    }

    private static IReadOnlyList<string> ParseResultsPage(JsonElement root)
    {
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            return ParseResultArray(results);

        var id = ParseResultId(root);
        return id is null ? [] : [id];
    }

    private static string? ParseResultId(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (TryGetString(item, "display_id", out var displayId))
            return displayId;

        return TryGetString(item, "source", out var source) && TryGetString(item, "source_id", out var sourceId)
            ? $"{source}:{sourceId}"
            : null;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        if (item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                value = text;
                return true;
            }
        }

        value = "";
        return false;
    }

    private static IReadOnlyList<string> ParseLines(string input)
        => input.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
