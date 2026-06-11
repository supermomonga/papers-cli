using System.Text.Json;
using PapersCli.Cli.Json;

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
                JsonValueKind.Array => ParseLegacyArray(input),
                JsonValueKind.Object => ParseResultsPage(input),
                _ => ParseLines(input),
            };
        }
        catch (JsonException)
        {
            return ParseLines(input);
        }
    }

    private static IReadOnlyList<string> ParseLegacyArray(string input)
    {
        var results = JsonSerializer.Deserialize(input, PapersJsonContext.Default.SearchResultArray);
        return results?.Select(r => r.DisplayId).ToList() ?? [];
    }

    private static IReadOnlyList<string> ParseResultsPage(string input)
    {
        var page = JsonSerializer.Deserialize(input, PapersJsonContext.Default.SearchResultsPage);
        return page?.Results.Select(r => r.DisplayId).ToList() ?? [];
    }

    private static IReadOnlyList<string> ParseLines(string input)
        => input.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
