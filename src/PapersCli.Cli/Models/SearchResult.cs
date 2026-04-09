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
