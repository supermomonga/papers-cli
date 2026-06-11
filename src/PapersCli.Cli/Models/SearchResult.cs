using System.Text.Json.Serialization;

namespace PapersCli.Cli.Models;

public record SearchResult
{
    public required string Source { get; init; }
    public required string SourceId { get; init; }
    public required string Title { get; init; }
    public required string Authors { get; init; }
    public string? PublishedAt { get; init; }
    public string? Abstract { get; init; }
    public required string Url { get; init; }
    public string? Doi { get; init; }
    public string? Journal { get; init; }
    public string? Categories { get; init; }
    public Dictionary<string, string> DownloadUrls { get; init; } = new();

    public string DisplayId => $"{Source}:{SourceId}";

    public int? PublishedYear
        => PublishedAt is not null && DateTime.TryParse(PublishedAt, out var dt)
            ? dt.Year
            : null;
}

public record SearchResultsPage
{
    [JsonPropertyOrder(0)]
    public required string Source { get; init; }

    [JsonPropertyOrder(1)]
    public required string Query { get; init; }

    [JsonPropertyOrder(8)]
    public required List<SearchResult> Results { get; init; }

    [JsonPropertyOrder(5)]
    public required int TotalResults { get; init; }

    [JsonPropertyOrder(2)]
    public required int Page { get; init; }

    [JsonPropertyOrder(3)]
    public required int Limit { get; init; }

    [JsonPropertyOrder(4)]
    public int ReturnedResults => Results.Count;

    [JsonPropertyOrder(6)]
    public int TotalPages => Limit > 0
        ? (int)Math.Ceiling((double)TotalResults / Limit)
        : 0;

    [JsonPropertyOrder(7)]
    public bool HasMore => Page < TotalPages;
}
