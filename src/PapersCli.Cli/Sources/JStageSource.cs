using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using PapersCli.Cli.Json;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Sources;

public partial class JStageSource(HttpClient httpClient, CiNiiSource cinii) : IPaperSource
{
    private const string BaseUrl = "https://api.jstage.jst.go.jp/searchapi/do";
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace PrismNs = "http://prismstandard.org/namespaces/basic/2.0/";
    private static readonly XNamespace OpenSearchNs = "http://a9.com/-/spec/opensearch/1.1/";

    public string Name => "jstage";
    public IReadOnlyList<string> SupportedFormats => ["pdf"];

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

        return await SearchJStageAsync(query, author, fromYear, toYear, sortKey, sortOrder, limit, page, cancellationToken);
    }

    public async Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        // Try J-STAGE API first (by DOI)
        var result = await GetMetadataFromJStageAsync(sourceId, cancellationToken);
        if (result is not null)
            return result;

        // Fallback: try CiNii (sourceId might be a CRID)
        var ciniiResult = await cinii.GetMetadataAsync(sourceId, cancellationToken);
        if (ciniiResult is not null)
            return ciniiResult with { Source = "jstage" };

        return null;
    }

    public async Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        // Try DOI resolution to get PDF URL
        var doi = sourceId.Contains('/') ? sourceId : null;

        if (doi is not null)
        {
            var doiUrl = $"https://doi.org/{doi}";
            try
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var resolveClient = new HttpClient(handler);
                var request = new HttpRequestMessage(HttpMethod.Head, doiUrl);
                var response = await resolveClient.SendAsync(request, cancellationToken);
                var resolvedUrl = response.RequestMessage?.RequestUri?.ToString();

                if (resolvedUrl is not null && resolvedUrl.Contains("jstage.jst.go.jp"))
                {
                    var pdfUrl = JStageArticleToPdfPattern().Replace(resolvedUrl, "/_pdf/");
                    return new Dictionary<string, string> { ["pdf"] = pdfUrl };
                }
            }
            catch (HttpRequestException) { }
        }

        return new Dictionary<string, string>();
    }

    public string? ParseUrl(string url)
    {
        var match = JStageArticleUrlPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<SearchResultsPage> SearchJStageAsync(
        string query, string? author, int? fromYear, int? toYear,
        string sortKey, string sortOrder, int limit, int page, CancellationToken cancellationToken)
    {
        var start = (page - 1) * limit + 1;
        var parameters = new List<string>
        {
            "service=3",
            $"article={HttpUtility.UrlEncode(query)}",
            $"count={limit}",
            $"start={start}",
        };

        if (!string.IsNullOrEmpty(author))
            parameters.Add($"author={HttpUtility.UrlEncode(author)}");
        if (fromYear.HasValue)
            parameters.Add($"pubyearfrom={fromYear.Value}");
        if (toYear.HasValue)
            parameters.Add($"pubyearto={toYear.Value}");

        parameters.Add(sortKey switch
        {
            "date" => "sortflg=2",
            "title" => "sortflg=5",
            _ => "sortflg=1",
        });

        var url = $"{BaseUrl}?{string.Join("&", parameters)}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);
            var totalResults = ParseIntElement(doc.Root, OpenSearchNs + "totalResults") ?? 0;

            var results = new List<SearchResult>();
            foreach (var entry in doc.Descendants(AtomNs + "entry"))
            {
                var result = ParseEntry(entry);
                if (result is not null)
                    results.Add(result);
            }
            return new SearchResultsPage
            {
                Source = Name,
                Query = query,
                Results = results,
                TotalResults = totalResults,
                Page = page,
                Limit = limit,
                SortKey = sortKey,
                SortOrder = sortOrder,
            };
        }
        catch (HttpRequestException)
        {
            return new SearchResultsPage
            {
                Source = Name,
                Query = query,
                Results = [],
                TotalResults = 0,
                Page = page,
                Limit = limit,
                SortKey = sortKey,
                SortOrder = sortOrder,
            };
        }
    }

    private async Task<SearchResult?> GetMetadataFromJStageAsync(string sourceId, CancellationToken cancellationToken)
    {
        var parameters = new List<string> { "service=3", "count=10" };

        if (sourceId.Contains('/'))
            parameters.Add($"article={HttpUtility.UrlEncode(sourceId)}");

        var url = $"{BaseUrl}?{string.Join("&", parameters)}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(xml);

            foreach (var entry in doc.Descendants(AtomNs + "entry"))
            {
                var doi = entry.Element(PrismNs + "doi")?.Value?.Trim();
                if (doi == sourceId)
                    return ParseEntry(entry);
            }

            var firstEntry = doc.Descendants(AtomNs + "entry").FirstOrDefault();
            return firstEntry is not null ? ParseEntry(firstEntry) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private SearchResult? ParseEntry(XElement entry)
    {
        var title = entry.Element(AtomNs + "article_title")?.Element(AtomNs + "ja")?.Value?.Trim()
            ?? entry.Element(AtomNs + "title")?.Value?.Trim()
            ?? "";

        var articleLink = entry.Element(AtomNs + "article_link")?.Element(AtomNs + "ja")?.Value?.Trim()
            ?? entry.Element(AtomNs + "link")?.Attribute("href")?.Value
            ?? "";

        var authorNames = entry.Element(AtomNs + "author")?.Element(AtomNs + "ja")?
            .Elements(AtomNs + "name")
            .Select(n => n.Value.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToArray() ?? [];

        var doi = entry.Element(PrismNs + "doi")?.Value?.Trim();
        var pubyear = entry.Element(AtomNs + "pubyear")?.Value?.Trim();
        var journal = entry.Element(AtomNs + "material_title")?.Element(AtomNs + "ja")?.Value?.Trim();

        var sourceId = doi;
        if (string.IsNullOrEmpty(sourceId) && !string.IsNullOrEmpty(articleLink))
            sourceId = ParseUrl(articleLink);
        if (string.IsNullOrEmpty(sourceId))
            return null;

        var downloadUrls = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(articleLink))
        {
            var pdfUrl = JStageArticleToPdfPattern().Replace(articleLink, "/_pdf/");
            downloadUrls["pdf"] = pdfUrl;
        }

        string? publishedAt = null;
        if (!string.IsNullOrEmpty(pubyear))
        {
            var year = pubyear.Split('-')[0];
            publishedAt = $"{year}-01-01";
        }

        return new SearchResult
        {
            Source = "jstage",
            SourceId = sourceId,
            Title = title,
            Authors = JsonSerializer.Serialize(authorNames, PapersJsonContext.Default.StringArray),
            PublishedAt = publishedAt,
            Url = articleLink,
            Doi = doi,
            Journal = journal,
            Categories = JsonSerializer.Serialize(Array.Empty<string>(), PapersJsonContext.Default.StringArray),
            DownloadUrls = downloadUrls,
        };
    }

    [GeneratedRegex(@"jstage\.jst\.go\.jp/article/((?:[^/]+/){3}[^/]+)")]
    private static partial Regex JStageArticleUrlPattern();

    [GeneratedRegex(@"/_article(?:/|$)")]
    private static partial Regex JStageArticleToPdfPattern();

    private static int? ParseIntElement(XElement? root, XName name)
        => int.TryParse(root?.Element(name)?.Value, out var value) ? value : null;
}
