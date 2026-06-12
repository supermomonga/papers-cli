using PapersCli.Cli.Data;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Tests.Data;

public class PaperRepositoryTests
{
    private static PaperRepository CreateRepository()
    {
        // Each test gets its own unique in-file SQLite DB
        var dbPath = Path.Combine(Path.GetTempPath(), $"papers-test-{Guid.NewGuid()}.db");
        return new PaperRepository($"Data Source={dbPath}");
    }

    private static Paper MakePaper(string source = "arxiv", string sourceId = "2301.00001") => new()
    {
        Source = source,
        SourceId = sourceId,
        Title = "Test Paper",
        Authors = ["Author A", "Author B"],
        PublishedAt = "2023-01-15T00:00:00Z",
        Abstract = "Test abstract",
        Url = $"https://arxiv.org/abs/{sourceId}",
        Doi = "10.1234/test",
        Journal = null,
        Categories = ["cs.AI"],
        CreatedAt = DateTime.UtcNow.ToString("o"),
    };

    [Test]
    public async Task InsertAndGetPaper_RoundTrip()
    {
        var repo = CreateRepository();
        var paper = MakePaper();

        var id = await repo.InsertPaperAsync(paper);
        await Assert.That(id).IsGreaterThan(0);

        var retrieved = await repo.GetPaperAsync("arxiv", "2301.00001");
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Title).IsEqualTo("Test Paper");
        await Assert.That(retrieved.Source).IsEqualTo("arxiv");
        await Assert.That(retrieved.SourceId).IsEqualTo("2301.00001");
        await Assert.That(retrieved.Doi).IsEqualTo("10.1234/test");
    }

    [Test]
    public async Task GetPaper_NotFound_ReturnsNull()
    {
        var repo = CreateRepository();
        var result = await repo.GetPaperAsync("arxiv", "nonexistent");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SearchPapers_ByQuery_FindsMatchingPapers()
    {
        var repo = CreateRepository();
        await repo.InsertPaperAsync(MakePaper("arxiv", "001") with { Title = "Attention is All You Need" });
        await repo.InsertPaperAsync(MakePaper("arxiv", "002") with { Title = "BERT: Pre-training" });

        var results = await repo.SearchPapersAsync(query: "Attention");
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Title).IsEqualTo("Attention is All You Need");
    }

    [Test]
    public async Task SearchPapers_BySource_FiltersCorrectly()
    {
        var repo = CreateRepository();
        await repo.InsertPaperAsync(MakePaper("arxiv", "001"));
        await repo.InsertPaperAsync(MakePaper("jstage", "002") with { Url = "https://jstage.jst.go.jp/article/002" });

        var results = await repo.SearchPapersAsync(source: "jstage");
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Source).IsEqualTo("jstage");
    }

    [Test]
    public async Task InsertAndGetPaperFile_RoundTrip()
    {
        var repo = CreateRepository();
        var paperId = await repo.InsertPaperAsync(MakePaper());

        var file = new PaperFile
        {
            PaperId = paperId,
            Format = "pdf",
            FilePath = "/tmp/test.pdf",
            SourceUrl = "https://arxiv.org/pdf/2301.00001",
            DownloadedAt = DateTime.UtcNow.ToString("o"),
        };
        await repo.InsertPaperFileAsync(file);

        var files = await repo.GetPaperFilesAsync(paperId);
        await Assert.That(files.Count).IsEqualTo(1);
        await Assert.That(files[0].Format).IsEqualTo("pdf");
        await Assert.That(files[0].FilePath).IsEqualTo("/tmp/test.pdf");
    }

    [Test]
    public async Task DeletePaper_CascadesDeleteToFiles()
    {
        var repo = CreateRepository();
        var paperId = await repo.InsertPaperAsync(MakePaper());

        await repo.InsertPaperFileAsync(new PaperFile
        {
            PaperId = paperId,
            Format = "pdf",
            FilePath = "/tmp/test.pdf",
            SourceUrl = "https://arxiv.org/pdf/2301.00001",
            DownloadedAt = DateTime.UtcNow.ToString("o"),
        });

        await repo.DeletePaperAsync(paperId);

        var paper = await repo.GetPaperAsync("arxiv", "2301.00001");
        await Assert.That(paper).IsNull();

        var files = await repo.GetPaperFilesAsync(paperId);
        await Assert.That(files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SearchPapers_WithLimit_RespectsLimit()
    {
        var repo = CreateRepository();
        for (var i = 0; i < 5; i++)
            await repo.InsertPaperAsync(MakePaper("arxiv", $"paper-{i}"));

        var results = await repo.SearchPapersAsync(limit: 3);
        await Assert.That(results.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetAllPaperFilesForSource_ReturnsCorrectFiles()
    {
        var repo = CreateRepository();
        var paperId = await repo.InsertPaperAsync(MakePaper());

        await repo.InsertPaperFileAsync(new PaperFile
        {
            PaperId = paperId, Format = "pdf",
            FilePath = "/tmp/test.pdf", SourceUrl = "https://example.com/test.pdf",
            DownloadedAt = DateTime.UtcNow.ToString("o"),
        });
        await repo.InsertPaperFileAsync(new PaperFile
        {
            PaperId = paperId, Format = "source",
            FilePath = "/tmp/test.tar.gz", SourceUrl = "https://example.com/test.tar.gz",
            DownloadedAt = DateTime.UtcNow.ToString("o"),
        });

        var files = await repo.GetAllPaperFilesForSourceAsync("arxiv", "2301.00001");
        await Assert.That(files.Count).IsEqualTo(2);
    }
}
