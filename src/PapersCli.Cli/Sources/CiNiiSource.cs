using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using PapersCli.Cli.Json;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Sources;

public partial class CiNiiSource(HttpClient httpClient) : IPaperSource
{
    private const string BaseUrl = "https://cir.nii.ac.jp/opensearch/articles";

    public string Name => "cinii";
    public IReadOnlyList<string> SupportedFormats => ["pdf"];

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        string? author = null,
        int? fromYear = null,
        int? toYear = null,
        string? category = null,
        string sort = "relevance",
        int limit = 20,
        CancellationToken cancellationToken = default)
        => await SearchAsync(query, author, fromYear, toYear, category, sort, limit, dataSourceType: null, cancellationToken);

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        string? author,
        int? fromYear,
        int? toYear,
        string? category,
        string sort,
        int limit,
        string? dataSourceType,
        CancellationToken cancellationToken)
    {
        var parameters = new List<string>
        {
            $"q={HttpUtility.UrlEncode(query)}",
            $"count={limit}",
            "format=json",
        };

        if (!string.IsNullOrEmpty(dataSourceType))
            parameters.Add($"dataSourceType={dataSourceType}");

        if (!string.IsNullOrEmpty(author))
            parameters.Add($"creator={HttpUtility.UrlEncode(author)}");
        if (fromYear.HasValue)
            parameters.Add($"from={fromYear.Value}");
        if (toYear.HasValue)
            parameters.Add($"until={toYear.Value}");

        var sortParam = sort switch
        {
            "date" => "sortorder=0",
            _ => "sortorder=1",
        };
        parameters.Add(sortParam);

        var url = $"{BaseUrl}?{string.Join("&", parameters)}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/json");
        var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseSearchResponse(json);
    }

    public async Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        // CiNii Research: Get metadata by CRID
        var url = $"https://cir.nii.ac.jp/crid/{sourceId}.json";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/json");

        var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseDetailResponse(json, sourceId);
    }

    public async Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var urls = new Dictionary<string, string>();

        // First, get metadata to find DOI
        var metadata = await GetMetadataAsync(sourceId, cancellationToken);
        if (metadata?.Doi is null)
            return urls;

        // Resolve DOI to actual hosting URL
        var doiUrl = $"https://doi.org/{metadata.Doi}";
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, doiUrl);
            var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var resolveClient = new HttpClient(handler);
            var response = await resolveClient.SendAsync(request, cancellationToken);
            var resolvedUrl = response.RequestMessage?.RequestUri?.ToString();

            if (resolvedUrl is not null && resolvedUrl.Contains("jstage.jst.go.jp"))
            {
                // J-STAGE: convert article URL to PDF URL
                var pdfUrl = Regex.Replace(resolvedUrl, @"/_article(?:/|$)", "/_pdf/");
                urls["pdf"] = pdfUrl;
            }
            else if (resolvedUrl is not null)
            {
                // Other hosting sites: use resolved URL as-is
                urls["pdf"] = resolvedUrl;
            }
        }
        catch (HttpRequestException)
        {
            // DOI resolution failed; no download URL available
        }

        return urls;
    }

    public string? ParseUrl(string url)
    {
        var match = CiNiiUrlPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private IReadOnlyList<SearchResult> ParseSearchResponse(string json)
    {
        var results = new List<SearchResult>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("items", out var items))
            return results;

        foreach (var item in items.EnumerateArray())
        {
            var result = ParseItem(item);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }

    private SearchResult? ParseDetailResponse(string json, string sourceId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Detail JSON-LD has different structure from OpenSearch
        // Title: dc:title array of {`@language`, `@value`}
        string title = "";
        if (root.TryGetProperty("dc:title", out var titleArray) && titleArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in titleArray.EnumerateArray())
            {
                if (t.TryGetProperty("@language", out var lang) && lang.GetString() == "ja"
                    && t.TryGetProperty("@value", out var val))
                {
                    title = val.GetString() ?? "";
                    break;
                }
            }
            if (string.IsNullOrEmpty(title))
            {
                var first = titleArray.EnumerateArray().FirstOrDefault();
                if (first.TryGetProperty("@value", out var val))
                    title = val.GetString() ?? "";
            }
        }

        // Authors: creator array of objects with foaf:name array
        var authors = new List<string>();
        if (root.TryGetProperty("creator", out var creators) && creators.ValueKind == JsonValueKind.Array)
        {
            foreach (var creator in creators.EnumerateArray())
            {
                if (creator.TryGetProperty("foaf:name", out var names) && names.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in names.EnumerateArray())
                    {
                        if (n.TryGetProperty("@language", out var lang) && lang.GetString() == "ja"
                            && n.TryGetProperty("@value", out var val))
                        {
                            var name = val.GetString();
                            if (name is not null) authors.Add(name);
                            break;
                        }
                    }
                }
            }
        }

        // DOI from productIdentifier
        string? doi = null;
        if (root.TryGetProperty("productIdentifier", out var identifiers) && identifiers.ValueKind == JsonValueKind.Array)
        {
            foreach (var id in identifiers.EnumerateArray())
            {
                if (id.TryGetProperty("identifier", out var ident)
                    && ident.TryGetProperty("@type", out var idType) && idType.GetString() == "DOI"
                    && ident.TryGetProperty("@value", out var idValue))
                {
                    doi = idValue.GetString();
                    break;
                }
            }
        }

        // Publication info
        string? published = null;
        string? journal = null;
        if (root.TryGetProperty("publication", out var pub))
        {
            if (pub.TryGetProperty("prism:publicationDate", out var pubDate))
                published = pubDate.GetString();
            if (pub.TryGetProperty("prism:publicationName", out var pubNames) && pubNames.ValueKind == JsonValueKind.Array)
            {
                foreach (var pn in pubNames.EnumerateArray())
                {
                    if (pn.TryGetProperty("@language", out var lang) && lang.GetString() == "ja"
                        && pn.TryGetProperty("@value", out var val))
                    {
                        journal = val.GetString();
                        break;
                    }
                }
            }
        }

        // Abstract
        string? description = null;
        if (root.TryGetProperty("description", out var descs) && descs.ValueKind == JsonValueKind.Array)
        {
            foreach (var desc in descs.EnumerateArray())
            {
                if (desc.TryGetProperty("notation", out var notations) && notations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in notations.EnumerateArray())
                    {
                        if (n.TryGetProperty("@value", out var val))
                        {
                            description = val.GetString();
                            break;
                        }
                    }
                }
            }
        }

        // Subjects
        var categories = new List<string>();
        if (root.TryGetProperty("foaf:topic", out var topics) && topics.ValueKind == JsonValueKind.Array)
        {
            foreach (var topic in topics.EnumerateArray())
            {
                if (topic.TryGetProperty("dc:title", out var topicTitle))
                {
                    var s = topicTitle.GetString();
                    if (s is not null) categories.Add(s);
                }
            }
        }

        return new SearchResult
        {
            Source = "cinii",
            SourceId = sourceId,
            Title = title,
            Authors = JsonSerializer.Serialize(authors, PapersJsonContext.Default.ListString),
            PublishedAt = published,
            Abstract = description,
            Url = $"https://cir.nii.ac.jp/crid/{sourceId}",
            Doi = doi,
            Journal = journal,
            Categories = JsonSerializer.Serialize(categories, PapersJsonContext.Default.ListString),
            DownloadUrls = new Dictionary<string, string>(),
        };
    }

    private SearchResult? ParseItem(JsonElement item, string? fallbackId = null)
    {
        var title = GetStringProperty(item, "title") ?? "";
        var link = GetStringProperty(item, "@id") ?? GetStringProperty(item, "url") ?? "";

        // Extract CRID from link
        var sourceId = fallbackId;
        if (sourceId is null && !string.IsNullOrEmpty(link))
            sourceId = ParseUrl(link);
        if (sourceId is null)
            return null;

        // dc:creator is a string array in CiNii JSON-LD
        var authors = new List<string>();
        if (item.TryGetProperty("dc:creator", out var creatorArray))
        {
            if (creatorArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var creator in creatorArray.EnumerateArray())
                {
                    var name = creator.GetString();
                    if (name is not null)
                        authors.Add(name);
                }
            }
            else if (creatorArray.ValueKind == JsonValueKind.String)
            {
                var name = creatorArray.GetString();
                if (name is not null)
                    authors.Add(name);
            }
        }

        var published = GetStringProperty(item, "prism:publicationDate");
        var description = GetStringProperty(item, "description");
        var journal = GetStringProperty(item, "prism:publicationName");

        // dc:identifier can contain DOI
        string? doi = null;
        if (item.TryGetProperty("dc:identifier", out var identifiers))
        {
            if (identifiers.ValueKind == JsonValueKind.Array)
            {
                foreach (var id in identifiers.EnumerateArray())
                {
                    if (id.TryGetProperty("@type", out var idType) && idType.GetString() == "cir:DOI"
                        && id.TryGetProperty("@value", out var idValue))
                    {
                        doi = idValue.GetString();
                        break;
                    }
                }
            }
        }

        // dc:subject as categories
        var categories = new List<string>();
        if (item.TryGetProperty("dc:subject", out var subjects) && subjects.ValueKind == JsonValueKind.Array)
        {
            foreach (var subject in subjects.EnumerateArray())
            {
                var s = subject.GetString();
                if (s is not null)
                    categories.Add(s);
            }
        }

        var downloadUrls = new Dictionary<string, string>();
        if (item.TryGetProperty("fullTextURL", out var fullTextUrl))
        {
            var pdfUrl = fullTextUrl.GetString();
            if (pdfUrl is not null)
                downloadUrls["pdf"] = pdfUrl;
        }

        return new SearchResult
        {
            Source = "cinii",
            SourceId = sourceId,
            Title = title,
            Authors = JsonSerializer.Serialize(authors, PapersJsonContext.Default.ListString),
            PublishedAt = published,
            Abstract = description,
            Url = link.StartsWith("http") ? link : $"https://cir.nii.ac.jp/crid/{sourceId}",
            Doi = doi,
            Journal = journal,
            Categories = JsonSerializer.Serialize(categories, PapersJsonContext.Default.ListString),
            DownloadUrls = downloadUrls,
        };
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Array => prop.EnumerateArray().FirstOrDefault().GetString(),
                _ => prop.ToString(),
            };
        }
        return null;
    }

    [GeneratedRegex(@"cir\.nii\.ac\.jp/crid/(\d+)")]
    private static partial Regex CiNiiUrlPattern();
}
