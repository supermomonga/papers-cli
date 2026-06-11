using PapersCli.Cli.Commands;
using PapersCli.Cli.Config;
using PapersCli.Cli.Data;
using PapersCli.Cli.Models;
using PapersCli.Cli.Sources;

namespace PapersCli.Cli.Tests.Commands;

public class SearchCommandTests
{
    [Test]
    [NotInParallel("Console")]
    public async Task Search_UsesConfigDefaultSourceAndPassesPagingOptions()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (_, output, _) = await CaptureConsoleAsync(() =>
            command.Search("attention", limit: 10, page: 2, json: true));

        await Assert.That(source.Calls).IsEqualTo(1);
        await Assert.That(source.LastQuery).IsEqualTo("attention");
        await Assert.That(source.LastLimit).IsEqualTo(10);
        await Assert.That(source.LastPage).IsEqualTo(2);
        await Assert.That(output.Contains("\"sort_key\": \"relevance\"")).IsTrue();
        await Assert.That(output.Contains("\"sort_order\": \"desc\"")).IsTrue();
        await Assert.That(output.Contains("\"total_results\": 30")).IsTrue();
        await Assert.That(output.Contains("\"total_pages\": 3")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsMultipleSources()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", source: "arxiv,jstage", json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("single source")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsUnknownSource()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", source: "jstage", json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("Unknown source: jstage")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsUnsupportedSort()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", sortKey: "author", json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("Sort 'author' is not supported by arxiv. Supported sort keys:")).IsTrue();
        await Assert.That(error.Contains("- relevance = asc|desc (default:desc)")).IsTrue();
        await Assert.That(error.Contains("- date = asc|desc (default:desc)")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_PrintsSortKeyAndOrderInSummary()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (_, output, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", sortKey: "date", sortOrder: "asc", json: false));

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output.Contains("sort: date=asc")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsUnsupportedJStageSortWithSupportedKeys()
    {
        var source = new FakeSource("jstage");
        var command = CreateCommand(new AppConfig { DefaultSource = "jstage" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", sortKey: "a", json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("Sort 'a' is not supported by jstage. Supported sort keys:")).IsTrue();
        await Assert.That(error.Contains("- relevance = desc (default:desc)")).IsTrue();
        await Assert.That(error.Contains("- date = desc (default:desc)")).IsTrue();
        await Assert.That(error.Contains("- title = asc (default:asc)")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsUnsupportedSortOrderWithSupportedKeys()
    {
        var source = new FakeSource("irdb");
        var command = CreateCommand(new AppConfig { DefaultSource = "irdb" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", sortKey: "date", sortOrder: "asc", json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("Sort order 'asc' is not supported for sort key 'date' by irdb. Supported sort keys:")).IsTrue();
        await Assert.That(error.Contains("- relevance = desc (default:desc)")).IsTrue();
        await Assert.That(error.Contains("- date = desc (default:desc)")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsInvalidLimit()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", limit: 0, json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("--limit must be greater than 0.")).IsTrue();
    }

    [Test]
    [NotInParallel("Console")]
    public async Task Search_RejectsInvalidPage()
    {
        var source = new FakeSource("arxiv");
        var command = CreateCommand(new AppConfig { DefaultSource = "arxiv" }, source);

        var (error, _, exitCode) = await CaptureConsoleAsync(() =>
            command.Search("attention", page: 0, json: true));

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(0);
        await Assert.That(error.Contains("--page must be greater than 0.")).IsTrue();
    }

    private static PaperCommands CreateCommand(AppConfig config, params IPaperSource[] sources)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"papers-command-test-{Guid.NewGuid()}.db");
        var repository = new PaperRepository($"Data Source={dbPath}");
        return new PaperCommands(repository, sources, config, new HttpClient());
    }

    private static async Task<(string Error, string Output, int ExitCode)> CaptureConsoleAsync(Func<Task> action)
    {
        Environment.ExitCode = 0;
        var previousError = Console.Error;
        var previousOutput = Console.Out;
        using var error = new StringWriter();
        using var output = new StringWriter();
#pragma warning disable TUnit0055
        Console.SetError(error);
        Console.SetOut(output);
#pragma warning restore TUnit0055
        try
        {
            await action();
            return (error.ToString(), output.ToString(), Environment.ExitCode);
        }
        finally
        {
#pragma warning disable TUnit0055
            Console.SetError(previousError);
            Console.SetOut(previousOutput);
#pragma warning restore TUnit0055
            Environment.ExitCode = 0;
        }
    }

    private sealed class FakeSource(string name) : IPaperSource
    {
        public string Name => name;
        public IReadOnlyList<string> SupportedFormats => ["pdf"];
        public int Calls { get; private set; }
        public string? LastQuery { get; private set; }
        public int? LastLimit { get; private set; }
        public int? LastPage { get; private set; }

        public Task<SearchResultsPage> SearchAsync(
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
            Calls++;
            LastQuery = query;
            LastLimit = limit;
            LastPage = page;

            return Task.FromResult(new SearchResultsPage
            {
                Source = name,
                Query = query,
                Page = page,
                Limit = limit,
                SortKey = sortKey,
                SortOrder = sortOrder ?? "desc",
                TotalResults = 30,
                Results =
                [
                    new SearchResult
                    {
                        Source = name,
                        SourceId = "id-1",
                        Title = "Paper",
                        Authors = "[]",
                        Url = "https://example.com/paper",
                    },
                ],
            });
        }

        public Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public string? ParseUrl(string url) => null;
    }
}
