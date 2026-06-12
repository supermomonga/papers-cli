using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Sources;

public partial class ArxivSource(HttpClient httpClient) : IPaperSource
{
    private const string BaseUrl = "https://export.arxiv.org/api/query";
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ArxivNs = "http://arxiv.org/schemas/atom";
    private static readonly XNamespace OpenSearchNs = "http://a9.com/-/spec/opensearch/1.1/";

    public string Name => "arxiv";
    public IReadOnlyList<string> SupportedFormats => ["pdf", "source"];

    public async Task<SearchResultsPage> SearchAsync(
        string query,
        string? author = null,
        int? fromYear = null,
        int? toYear = null,
        string? category = null,
        string sortKey = "relevance",
        string? sortOrder = null,
        int limit = 20,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        sortKey = SearchSortOptions.Normalize(sortKey);
        sortOrder = SearchSortOptions.ResolveAndValidate(Name, sortKey, sortOrder);

        var searchQuery = BuildSearchQuery(query, author, category, fromYear, toYear);
        var sortBy = sortKey switch
        {
            "date" => "submittedDate",
            "relevance" => "relevance",
            _ => "relevance",
        };
        var apiSortOrder = sortOrder == SearchSortOptions.Asc ? "ascending" : "descending";
        var start = (page - 1) * limit;

        var url = $"{BaseUrl}?search_query={HttpUtility.UrlEncode(searchQuery)}&start={start}&max_results={limit}&sortBy={sortBy}&sortOrder={apiSortOrder}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, delayMs: 3000, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = XDocument.Parse(xml);
        var totalResults = ParseIntElement(doc.Root, OpenSearchNs + "totalResults");

        var results = new List<SearchResult>();
        foreach (var entry in doc.Descendants(AtomNs + "entry"))
        {
            var result = ParseEntry(entry);
            if (result is null) continue;

            results.Add(result);
        }

        return new SearchResultsPage
        {
            Source = Name,
            Query = query,
            Results = results,
            TotalResults = totalResults ?? results.Count,
            Page = page,
            Limit = limit,
            SortKey = sortKey,
            SortOrder = sortOrder,
        };
    }

    public async Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?id_list={HttpUtility.UrlEncode(sourceId)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, delayMs: 3000, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = XDocument.Parse(xml);

        var entry = doc.Descendants(AtomNs + "entry").FirstOrDefault();
        return entry is not null ? ParseEntry(entry) : null;
    }

    public Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var urls = new Dictionary<string, string>
        {
            ["pdf"] = $"https://arxiv.org/pdf/{sourceId}",
            ["source"] = $"https://arxiv.org/e-print/{sourceId}",
        };
        return Task.FromResult(urls);
    }

    public string? ParseUrl(string url)
    {
        var match = ArxivUrlPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string BuildSearchQuery(string query, string? author, string? category, int? fromYear, int? toYear)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(query))
            parts.Add($"all:{query}");
        if (!string.IsNullOrEmpty(author))
            parts.Add($"au:{author}");
        if (!string.IsNullOrEmpty(category))
            parts.Add($"cat:{category}");
        if (fromYear.HasValue || toYear.HasValue)
        {
            var fromDate = fromYear.HasValue ? $"{fromYear.Value:0000}01010000" : "199101010000";
            var toDate = toYear.HasValue ? $"{toYear.Value:0000}12312359" : "999912312359";
            parts.Add($"submittedDate:[{fromDate} TO {toDate}]");
        }

        return parts.Count > 0 ? string.Join(" AND ", parts) : "all:*";
    }

    private static int? ParseIntElement(XElement? root, XName name)
        => int.TryParse(root?.Element(name)?.Value, out var value) ? value : null;

    private SearchResult? ParseEntry(XElement entry)
    {
        var id = entry.Element(AtomNs + "id")?.Value;
        if (id is null) return null;

        var sourceId = ExtractArxivId(id);
        if (sourceId is null) return null;

        var title = entry.Element(AtomNs + "title")?.Value?.Trim().Replace("\n", " ").Replace("  ", " ") ?? "";
        var summary = entry.Element(AtomNs + "summary")?.Value?.Trim() ?? "";
        var published = entry.Element(AtomNs + "published")?.Value;

        var authors = entry.Elements(AtomNs + "author")
            .Select(a => a.Element(AtomNs + "name")?.Value)
            .OfType<string>()
            .ToArray();

        var categories = entry.Elements(AtomNs + "category")
            .Select(c => c.Attribute("term")?.Value)
            .OfType<string>()
            .ToArray();

        var doi = entry.Elements(ArxivNs + "doi").FirstOrDefault()?.Value;
        var journal = entry.Elements(ArxivNs + "journal_ref").FirstOrDefault()?.Value;

        var pdfLink = entry.Elements(AtomNs + "link")
            .FirstOrDefault(l => l.Attribute("title")?.Value == "pdf")
            ?.Attribute("href")?.Value;

        var downloadUrls = new Dictionary<string, string>();
        if (pdfLink is not null)
            downloadUrls["pdf"] = pdfLink;
        downloadUrls["source"] = $"https://arxiv.org/e-print/{sourceId}";

        return new SearchResult
        {
            Source = "arxiv",
            SourceId = sourceId,
            Title = title,
            Authors = authors,
            PublishedAt = published,
            Abstract = summary,
            Url = $"https://arxiv.org/abs/{sourceId}",
            Doi = doi,
            Journal = journal,
            Categories = categories,
            DownloadUrls = downloadUrls,
        };
    }

    private static string? ExtractArxivId(string idUrl)
    {
        // http://arxiv.org/abs/2301.00001v1 -> 2301.00001v1
        var match = ArxivIdPattern().Match(idUrl);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"arxiv\.org/(?:abs|pdf|e-print)/(\d{4}\.\d{4,5}(?:v\d+)?)")]
    private static partial Regex ArxivUrlPattern();

    [GeneratedRegex(@"arxiv\.org/abs/(\d{4}\.\d{4,5}(?:v\d+)?)")]
    private static partial Regex ArxivIdPattern();
}
