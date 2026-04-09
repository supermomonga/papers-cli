using System.Text.Json;
using PapersCli.Cli.Json;

namespace PapersCli.Cli.Models;

public record Paper
{
    public long Id { get; init; }
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
    public required string CreatedAt { get; init; }

    public IReadOnlyList<string> GetAuthorsList()
        => JsonSerializer.Deserialize(Authors, PapersJsonContext.Default.StringArray) ?? [];

    public IReadOnlyList<string> GetCategoriesList()
        => string.IsNullOrEmpty(Categories)
            ? []
            : JsonSerializer.Deserialize(Categories, PapersJsonContext.Default.StringArray) ?? [];

    public string DisplayId => $"{Source}:{SourceId}";

    public int? PublishedYear
        => PublishedAt is not null && DateTime.TryParse(PublishedAt, out var dt)
            ? dt.Year
            : null;
}
