using System.Globalization;
using System.Text.Json;
using ConsoleAppFramework;
using PapersCli.Cli.Config;
using PapersCli.Cli.Data;
using PapersCli.Cli.Json;
using PapersCli.Cli.Models;
using PapersCli.Cli.Sources;
using Spectre.Console;

namespace PapersCli.Cli.Commands;

public class PaperCommands(
    PaperRepository repository,
    IEnumerable<IPaperSource> sources,
    AppConfig config,
    HttpClient httpClient)
{
    private readonly Dictionary<string, IPaperSource> _sourceMap =
        sources.ToDictionary(s => s.Name, s => s);

    /// <summary>
    /// Search papers from remote sources.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="source">-s, Source name (e.g. arxiv,jstage,irdb). Defaults to config default-source.</param>
    /// <param name="author">-a, Filter by author name.</param>
    /// <param name="from">Filter by start year.</param>
    /// <param name="to">Filter by end year.</param>
    /// <param name="category">-c, Filter by category (e.g. cs.AI).</param>
    /// <param name="sort">Sort order: relevance, date, title.</param>
    /// <param name="limit">-l, Number of results per page.</param>
    /// <param name="page">Page number, starting from 1.</param>
    /// <param name="json">Output as JSON.</param>
    [Command("search")]
    public async Task Search(
        [Argument] string query,
        string? source = null,
        string? author = null,
        int? from = null,
        int? to = null,
        string? category = null,
        string sort = "relevance",
        int limit = 20,
        int page = 1,
        bool json = false)
    {
        if (limit <= 0)
        {
            await Console.Error.WriteLineAsync("--limit must be greater than 0.");
            Environment.ExitCode = 1;
            return;
        }

        if (page <= 0)
        {
            await Console.Error.WriteLineAsync("--page must be greater than 0.");
            Environment.ExitCode = 1;
            return;
        }

        var sourceName = string.IsNullOrWhiteSpace(source) ? config.DefaultSource : source.Trim();
        if (sourceName.Contains(','))
        {
            await Console.Error.WriteLineAsync("--source accepts a single source name. Multiple sources are no longer supported.");
            Environment.ExitCode = 1;
            return;
        }

        if (!_sourceMap.TryGetValue(sourceName, out var src))
        {
            await Console.Error.WriteLineAsync($"Unknown source: {sourceName}");
            Environment.ExitCode = 1;
            return;
        }

        sort = sort.ToLowerInvariant();
        if (!IsSortSupported(sourceName, sort))
        {
            await Console.Error.WriteLineAsync($"Sort '{sort}' is not supported by {sourceName}.");
            Environment.ExitCode = 1;
            return;
        }

        var resultsPage = await src.SearchAsync(query, author, from, to, category, sort, limit, page);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(resultsPage, PapersJsonContext.Default.SearchResultsPage));
            return;
        }

        if (resultsPage.Results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found.[/]");
            if (resultsPage.TotalResults > 0)
                AnsiConsole.MarkupLine($"\n[dim]{FormatSearchSummary(resultsPage)}[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Source:ID");
        table.AddColumn("Title");
        table.AddColumn("Authors");
        table.AddColumn("Year");
        table.AddColumn("Categories");
        table.AddColumn("DL");

        foreach (var r in resultsPage.Results)
        {
            var dlFormats = await GetDownloadedFormats(r.Source, r.SourceId);
            table.AddRow(
                Markup.Escape(r.DisplayId),
                Markup.Escape(Truncate(r.Title, 50)),
                Markup.Escape(Truncate(FormatAuthors(r.Authors), 30)),
                r.PublishedYear?.ToString() ?? "-",
                Markup.Escape(Truncate(FormatCategories(r.Categories), 20)),
                dlFormats.Count > 0 ? $"[green]{Markup.Escape(string.Join(",", dlFormats))}[/]" : "-");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[dim]{FormatSearchSummary(resultsPage)}[/]");
    }

    /// <summary>
    /// Download papers by ID or URL.
    /// </summary>
    /// <param name="format">-f, Comma-separated formats to download (e.g. pdf,source).</param>
    /// <param name="force">Force re-download even if already downloaded.</param>
    /// <param name="stdin">Read identifiers from stdin.</param>
    /// <param name="json">Output as JSON.</param>
    /// <param name="ids">Paper identifiers (source:id or URL).</param>
    [Command("download")]
    public async Task Download(
        string? format = null,
        bool force = false,
        bool stdin = false,
        bool json = false,
        [Argument] params string[] ids)
    {
        var allIds = ids.ToList();
        if (stdin)
        {
            var input = await Console.In.ReadToEndAsync();
            allIds.AddRange(SearchResultInputParser.ParseIds(input));
        }

        if (allIds.Count == 0)
        {
            await Console.Error.WriteLineAsync("No paper identifiers provided.");
            Environment.ExitCode = 1;
            return;
        }

        var requestedFormats = format?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? ["pdf"];

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                for (var i = 0; i < allIds.Count; i++)
                {
                    var task = ctx.AddTask($"[{i + 1}/{allIds.Count}] {Markup.Escape(allIds[i])}");

                    try
                    {
                        var (sourceName, sourceId) = PaperIdParser.Parse(allIds[i], sources);
                        await DownloadPaper(sourceName, sourceId, requestedFormats, force, task);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                        task.Value = task.MaxValue;
                    }
                }
            });
    }

    /// <summary>
    /// List downloaded papers.
    /// </summary>
    /// <param name="query">Optional search query for title/authors/abstract.</param>
    /// <param name="source">-s, Filter by source name.</param>
    /// <param name="author">-a, Filter by author name.</param>
    /// <param name="from">Filter by start year.</param>
    /// <param name="to">Filter by end year.</param>
    /// <param name="category">-c, Filter by category.</param>
    /// <param name="sort">Sort order: date, downloaded_at, title, author.</param>
    /// <param name="limit">-l, Maximum number of results.</param>
    /// <param name="json">Output as JSON.</param>
    [Command("list")]
    public async Task List(
        [Argument] string? query = null,
        string? source = null,
        string? author = null,
        int? from = null,
        int? to = null,
        string? category = null,
        string sort = "date",
        int limit = 20,
        bool json = false)
    {
        var papers = await repository.SearchPapersAsync(query, source, author, from, to, category, sort, limit);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(papers, PapersJsonContext.Default.IReadOnlyListPaper));
            return;
        }

        if (papers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No downloaded papers found.[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Source:ID");
        table.AddColumn("Title");
        table.AddColumn("Authors");
        table.AddColumn("Year");
        table.AddColumn("Categories");
        table.AddColumn("DL");

        foreach (var p in papers)
        {
            var files = await repository.GetPaperFilesAsync(p.Id);
            var formats = files.Select(f => f.Format).ToList();
            table.AddRow(
                Markup.Escape(p.DisplayId),
                Markup.Escape(Truncate(p.Title, 50)),
                Markup.Escape(Truncate(FormatAuthors(p.Authors), 30)),
                p.PublishedYear?.ToString() ?? "-",
                Markup.Escape(Truncate(FormatCategories(p.Categories), 20)),
                formats.Count > 0 ? $"[green]{Markup.Escape(string.Join(",", formats))}[/]" : "-");
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[dim]{papers.Count} papers found.[/]");
    }

    /// <summary>
    /// Show paper details.
    /// </summary>
    /// <param name="id">Paper identifier (source:id).</param>
    /// <param name="json">Output as JSON.</param>
    [Command("show")]
    public async Task Show(
        [Argument] string id,
        bool json = false)
    {
        var (sourceName, sourceId) = PaperIdParser.Parse(id, sources);
        var paper = await repository.GetPaperAsync(sourceName, sourceId);

        if (paper is null)
        {
            await Console.Error.WriteLineAsync($"Paper not found: {id}");
            Environment.ExitCode = 1;
            return;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(paper, PapersJsonContext.Default.Paper));
            return;
        }

        var files = await repository.GetPaperFilesAsync(paper.Id);

        var panel = new Panel(BuildDetailGrid(paper, files))
        {
            Header = new PanelHeader($" {Markup.Escape(paper.Title)} "),
            Border = BoxBorder.Rounded,
        };

        AnsiConsole.Write(panel);

        if (!string.IsNullOrEmpty(paper.Abstract))
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Abstract:[/]");
            AnsiConsole.WriteLine(paper.Abstract);
        }
    }

    /// <summary>
    /// Delete a downloaded paper.
    /// </summary>
    /// <param name="id">Paper identifier (source:id).</param>
    /// <param name="yes">-y, Skip confirmation prompt.</param>
    /// <param name="json">Output as JSON.</param>
    [Command("delete")]
    public async Task Delete(
        [Argument] string id,
        bool yes = false,
        bool json = false)
    {
        var (sourceName, sourceId) = PaperIdParser.Parse(id, sources);
        var paper = await repository.GetPaperAsync(sourceName, sourceId);

        if (paper is null)
        {
            await Console.Error.WriteLineAsync($"Paper not found: {id}");
            Environment.ExitCode = 1;
            return;
        }

        if (!yes)
        {
            var confirm = AnsiConsole.Confirm($"Delete [bold]{Markup.Escape(paper.Title)}[/]?", defaultValue: false);
            if (!confirm) return;
        }

        var files = await repository.GetPaperFilesAsync(paper.Id);
        foreach (var file in files)
        {
            if (File.Exists(file.FilePath))
                File.Delete(file.FilePath);
        }

        await repository.DeletePaperAsync(paper.Id);

        if (json)
            Console.WriteLine(JsonSerializer.Serialize(new DeleteResult(paper.DisplayId), PapersJsonContext.Default.DeleteResult));
        else
            AnsiConsole.MarkupLine($"[green]Deleted {Markup.Escape(paper.DisplayId)}[/]");
    }

    private async Task DownloadPaper(string sourceName, string sourceId, string[] formats, bool force, ProgressTask task)
    {
        if (!_sourceMap.TryGetValue(sourceName, out var src))
            throw new ArgumentException($"Unknown source: {sourceName}");

        var existingPaper = await repository.GetPaperAsync(sourceName, sourceId);
        if (existingPaper is not null && !force)
        {
            var existingFiles = await repository.GetPaperFilesAsync(existingPaper.Id);
            var existingFormats = existingFiles.Select(f => f.Format).ToHashSet();
            var newFormats = formats.Where(f => !existingFormats.Contains(f)).ToArray();
            if (newFormats.Length == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Already downloaded: {Markup.Escape($"{sourceName}:{sourceId}")}. Use --force to re-download.[/]");
                task.Value = task.MaxValue;
                return;
            }
            formats = newFormats;
        }

        task.Description = $"Fetching metadata for {sourceName}:{sourceId}...";
        var metadata = await src.GetMetadataAsync(sourceId);
        if (metadata is null)
            throw new InvalidOperationException($"Could not fetch metadata for {sourceName}:{sourceId}");

        task.MaxValue = formats.Length * 2;

        var downloadUrls = metadata.DownloadUrls.Count > 0
            ? metadata.DownloadUrls
            : await src.GetDownloadUrlsAsync(sourceId);

        long paperId;
        if (existingPaper is not null)
        {
            paperId = existingPaper.Id;
        }
        else
        {
            var paper = new Paper
            {
                Source = sourceName,
                SourceId = sourceId,
                Title = metadata.Title,
                Authors = metadata.Authors,
                PublishedAt = metadata.PublishedAt,
                Abstract = metadata.Abstract,
                Url = metadata.Url,
                Doi = metadata.Doi,
                Journal = metadata.Journal,
                Categories = metadata.Categories,
                CreatedAt = DateTime.UtcNow.ToString("o"),
            };
            paperId = await repository.InsertPaperAsync(paper);
        }

        task.Increment(1);

        foreach (var fmt in formats)
        {
            if (!downloadUrls.TryGetValue(fmt, out var dlUrl))
            {
                AnsiConsole.MarkupLine($"[yellow]Format '{fmt}' not available for {sourceName}:{sourceId}[/]");
                task.Increment(1);
                continue;
            }

            task.Description = $"Downloading {fmt} for {sourceName}:{sourceId}...";

            var sourceDir = Path.Combine(config.ResolvedDownloadDir, sourceName);
            Directory.CreateDirectory(sourceDir);

            var extension = fmt switch
            {
                "pdf" => ".pdf",
                "source" => ".tar.gz",
                "latex" => ".tar.gz",
                _ => $".{fmt}",
            };
            var filePath = Path.Combine(sourceDir, $"{SanitizeFileName(sourceId)}{extension}");

            using var stream = await HttpRetryHandler.DownloadWithRetryAsync(httpClient, dlUrl);
            using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream);

            var paperFile = new PaperFile
            {
                PaperId = paperId,
                Format = fmt,
                FilePath = filePath,
                SourceUrl = dlUrl,
                DownloadedAt = DateTime.UtcNow.ToString("o"),
            };
            await repository.InsertPaperFileAsync(paperFile);

            task.Increment(1);
        }

        task.Value = task.MaxValue;
        AnsiConsole.MarkupLine($"[green]Downloaded: {Markup.Escape($"{sourceName}:{sourceId}")}[/]");
    }

    private async Task<IReadOnlyList<string>> GetDownloadedFormats(string source, string sourceId)
    {
        var files = await repository.GetAllPaperFilesForSourceAsync(source, sourceId);
        return files.Select(f => f.Format).ToList();
    }

    private static bool IsSortSupported(string sourceName, string sort) => sourceName switch
    {
        "arxiv" => sort is "relevance" or "date",
        "irdb" => sort is "relevance" or "date",
        "jstage" => sort is "relevance" or "date" or "title",
        _ => sort is "relevance",
    };

    private static string FormatSearchSummary(SearchResultsPage page)
    {
        var totalResults = page.TotalResults.ToString("N0", CultureInfo.InvariantCulture);
        var totalPages = page.TotalPages.ToString("N0", CultureInfo.InvariantCulture);
        return $"Showing {page.ReturnedResults} of {totalResults} results (page {page.Page}/{totalPages}).";
    }

    private static Grid BuildDetailGrid(Paper paper, IReadOnlyList<PaperFile> files)
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(12));
        grid.AddColumn(new GridColumn());

        grid.AddRow("[bold]Source:[/]", Markup.Escape(paper.Source));
        grid.AddRow("[bold]ID:[/]", Markup.Escape(paper.SourceId));
        grid.AddRow("[bold]Authors:[/]", Markup.Escape(FormatAuthors(paper.Authors)));
        grid.AddRow("[bold]Published:[/]", Markup.Escape(paper.PublishedAt ?? "-"));
        if (paper.Doi is not null)
            grid.AddRow("[bold]DOI:[/]", Markup.Escape(paper.Doi));
        if (paper.Journal is not null)
            grid.AddRow("[bold]Journal:[/]", Markup.Escape(paper.Journal));
        grid.AddRow("[bold]Categories:[/]", Markup.Escape(FormatCategories(paper.Categories)));
        grid.AddRow("[bold]URL:[/]", Markup.Escape(paper.Url));

        foreach (var file in files)
            grid.AddRow($"[bold]{Markup.Escape(file.Format.ToUpper())}:[/]", Markup.Escape(file.FilePath));

        return grid;
    }

    private static string FormatAuthors(string authorsJson)
    {
        try
        {
            var authors = JsonSerializer.Deserialize(authorsJson, PapersJsonContext.Default.StringArray);
            if (authors is null || authors.Length == 0) return "-";
            return authors.Length <= 3
                ? string.Join(", ", authors)
                : $"{string.Join(", ", authors.Take(3))}, ...";
        }
        catch { return authorsJson; }
    }

    private static string FormatCategories(string? categoriesJson)
    {
        if (string.IsNullOrEmpty(categoriesJson)) return "-";
        try
        {
            var cats = JsonSerializer.Deserialize(categoriesJson, PapersJsonContext.Default.StringArray);
            return cats is null || cats.Length == 0 ? "-" : string.Join(", ", cats);
        }
        catch { return categoriesJson; }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    private static string SanitizeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
