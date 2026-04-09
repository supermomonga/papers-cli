using PapersCli.Cli.Models;

namespace PapersCli.Cli.Sources;

public interface IPaperSource
{
    string Name { get; }

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        string? author = null,
        int? fromYear = null,
        int? toYear = null,
        string? category = null,
        string sort = "relevance",
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<SearchResult?> GetMetadataAsync(
        string sourceId,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, string>> GetDownloadUrlsAsync(
        string sourceId,
        CancellationToken cancellationToken = default);

    string? ParseUrl(string url);

    IReadOnlyList<string> SupportedFormats { get; }
}
