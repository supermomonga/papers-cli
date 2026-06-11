using System.Text.Json;
using System.Text.RegularExpressions;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Sources;

/// <summary>
/// IRDB (Institutional Repository DataBase) source.
/// Searches via CiNii Research with dataSourceType=IRDB.
/// PDFs are hosted on university institutional repositories.
/// </summary>
public partial class IrdbSource(HttpClient httpClient, CiNiiSource cinii) : IPaperSource
{
    public string Name => "irdb";
    public IReadOnlyList<string> SupportedFormats => ["pdf"];

    public async Task<SearchResultsPage> SearchAsync(
        string query,
        string? author = null,
        int? fromYear = null,
        int? toYear = null,
        string? category = null,
        string sort = "relevance",
        int limit = 20,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (sort is not "relevance" and not "date")
            throw new ArgumentException($"Sort '{sort}' is not supported by irdb. Supported sort keys: relevance, date.");

        var resultsPage = await cinii.SearchAsync(
            query, author, fromYear, toYear, category, sort, limit, page,
            dataSourceType: "IRDB", cancellationToken);

        // Re-source as "irdb"
        return resultsPage with
        {
            Source = Name,
            Results = resultsPage.Results.Select(r => r with { Source = "irdb", SourceId = r.SourceId }).ToList(),
        };
    }

    public async Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var result = await cinii.GetMetadataAsync(sourceId, cancellationToken);
        return result is not null ? result with { Source = "irdb" } : null;
    }

    public async Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var urls = new Dictionary<string, string>();

        // Get metadata to find repository URL
        var metadata = await cinii.GetMetadataAsync(sourceId, cancellationToken);
        if (metadata is null)
            return urls;

        // First try DOI resolution
        if (metadata.Doi is not null)
        {
            var pdfUrl = await ResolvePdfFromDoiAsync(metadata.Doi, cancellationToken);
            if (pdfUrl is not null)
            {
                urls["pdf"] = pdfUrl;
                return urls;
            }
        }

        // Fallback: fetch the repository page and extract citation_pdf_url
        var repoUrl = await GetRepositoryUrlAsync(sourceId, cancellationToken);
        if (repoUrl is not null)
        {
            var pdfUrl = await ExtractPdfUrlFromRepoPageAsync(repoUrl, cancellationToken);
            if (pdfUrl is not null)
                urls["pdf"] = pdfUrl;
        }

        return urls;
    }

    public string? ParseUrl(string url)
    {
        // Match various institutional repository URL patterns
        // e.g., https://xxx.repo.nii.ac.jp/records/12345
        var match = RepoNiiPattern().Match(url);
        if (match.Success) return match.Groups[1].Value;

        // CiNii CRID URL
        var ciniiMatch = CiNiiCridPattern().Match(url);
        return ciniiMatch.Success ? ciniiMatch.Groups[1].Value : null;
    }

    private async Task<string?> GetRepositoryUrlAsync(string sourceId, CancellationToken cancellationToken)
    {
        // Get detail JSON from CiNii to find repository URL
        var url = $"https://cir.nii.ac.jp/crid/{sourceId}.json";
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("url", out var urlArray) && urlArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var u in urlArray.EnumerateArray())
                {
                    if (u.TryGetProperty("@id", out var idProp))
                    {
                        var repoUrl = idProp.GetString();
                        // Prefer .repo.nii.ac.jp URLs (IRDB repos)
                        if (repoUrl?.Contains("repo.nii.ac.jp") == true)
                            return repoUrl;
                    }
                }
                // Fallback: return first URL
                var first = urlArray.EnumerateArray().FirstOrDefault();
                if (first.TryGetProperty("@id", out var firstId))
                    return firstId.GetString();
            }
        }
        catch (Exception) { }

        return null;
    }

    private async Task<string?> ExtractPdfUrlFromRepoPageAsync(string repoUrl, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, repoUrl);
            var response = await HttpRetryHandler.SendWithRetryAsync(httpClient, request, cancellationToken: cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Extract citation_pdf_url from meta tag
            var match = CitationPdfUrlPattern().Match(html);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception) { return null; }
    }

    private async Task<string?> ResolvePdfFromDoiAsync(string doi, CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var resolveClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Head, $"https://doi.org/{doi}");
            var response = await resolveClient.SendAsync(request, cancellationToken);
            var resolvedUrl = response.RequestMessage?.RequestUri?.ToString();

            if (resolvedUrl is null) return null;

            // If resolved to a repo page, extract PDF URL from it
            if (resolvedUrl.Contains("repo.nii.ac.jp"))
                return await ExtractPdfUrlFromRepoPageAsync(resolvedUrl, cancellationToken);

            // If resolved to a direct PDF
            if (resolvedUrl.EndsWith(".pdf"))
                return resolvedUrl;

            return null;
        }
        catch (Exception) { return null; }
    }

    [GeneratedRegex(@"repo\.nii\.ac\.jp/records?/(\d+)")]
    private static partial Regex RepoNiiPattern();

    [GeneratedRegex(@"cir\.nii\.ac\.jp/crid/(\d+)")]
    private static partial Regex CiNiiCridPattern();

    [GeneratedRegex(@"<meta\s+name=""citation_pdf_url""\s+content=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CitationPdfUrlPattern();
}
