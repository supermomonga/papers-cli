using PapersCli.Cli.Models;
using PapersCli.Cli.Sources;

namespace PapersCli.Cli.Tests.Sources;

public class PaperIdParserTests
{
    private static readonly IEnumerable<IPaperSource> Sources = [new FakeArxivSource(), new FakeJStageSource(), new FakeCiNiiSource()];

    [Test]
    public async Task Parse_SourceColonId_ReturnsSourceAndId()
    {
        var (source, id) = PaperIdParser.Parse("arxiv:2301.00001", Sources);
        await Assert.That(source).IsEqualTo("arxiv");
        await Assert.That(id).IsEqualTo("2301.00001");
    }

    [Test]
    public async Task Parse_JStageSourceColonId_ReturnsSourceAndId()
    {
        var (source, id) = PaperIdParser.Parse("jstage:some/article/id", Sources);
        await Assert.That(source).IsEqualTo("jstage");
        await Assert.That(id).IsEqualTo("some/article/id");
    }

    [Test]
    public async Task Parse_ArxivUrl_ExtractsId()
    {
        var (source, id) = PaperIdParser.Parse("https://arxiv.org/abs/2301.00001", Sources);
        await Assert.That(source).IsEqualTo("arxiv");
        await Assert.That(id).IsEqualTo("2301.00001");
    }

    [Test]
    public async Task Parse_ArxivPdfUrl_ExtractsId()
    {
        var (source, id) = PaperIdParser.Parse("https://arxiv.org/pdf/2301.00001v2", Sources);
        await Assert.That(source).IsEqualTo("arxiv");
        await Assert.That(id).IsEqualTo("2301.00001v2");
    }

    [Test]
    public async Task Parse_CiNiiUrl_ExtractsId()
    {
        var (source, id) = PaperIdParser.Parse("https://cir.nii.ac.jp/crid/1234567890", Sources);
        await Assert.That(source).IsEqualTo("cinii");
        await Assert.That(id).IsEqualTo("1234567890");
    }

    [Test]
    public async Task Parse_EmptyString_ThrowsArgumentException()
    {
        await Assert.That(() => PaperIdParser.Parse("", Sources)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Parse_UnrecognizedUrl_ThrowsArgumentException()
    {
        await Assert.That(() => PaperIdParser.Parse("https://example.com/unknown", Sources)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Parse_InvalidFormat_ThrowsArgumentException()
    {
        await Assert.That(() => PaperIdParser.Parse("nocolon", Sources)).Throws<ArgumentException>();
    }

    // Minimal fakes that only implement ParseUrl
    private class FakeArxivSource : IPaperSource
    {
        private readonly ArxivSource _inner = new(new HttpClient());
        public string Name => "arxiv";
        public IReadOnlyList<string> SupportedFormats => ["pdf"];
        public string? ParseUrl(string url) => _inner.ParseUrl(url);
        public Task<SearchResultsPage> SearchAsync(string query, string? author, int? fromYear, int? toYear, string? category, string sortKey, string? sortOrder, int limit, int page, CancellationToken ct) => throw new NotImplementedException();
        public Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
    }

    private class FakeJStageSource : IPaperSource
    {
        private readonly JStageSource _inner = new(new HttpClient(), new CiNiiSource(new HttpClient()));
        public string Name => "jstage";
        public IReadOnlyList<string> SupportedFormats => ["pdf"];
        public string? ParseUrl(string url) => _inner.ParseUrl(url);
        public Task<SearchResultsPage> SearchAsync(string query, string? author, int? fromYear, int? toYear, string? category, string sortKey, string? sortOrder, int limit, int page, CancellationToken ct) => throw new NotImplementedException();
        public Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
    }

    private class FakeCiNiiSource : IPaperSource
    {
        private readonly CiNiiSource _inner = new(new HttpClient());
        public string Name => "cinii";
        public IReadOnlyList<string> SupportedFormats => ["pdf"];
        public string? ParseUrl(string url) => _inner.ParseUrl(url);
        public Task<SearchResultsPage> SearchAsync(string query, string? author, int? fromYear, int? toYear, string? category, string sortKey, string? sortOrder, int limit, int page, CancellationToken ct) => throw new NotImplementedException();
        public Task<SearchResult?> GetMetadataAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Dictionary<string, string>> GetDownloadUrlsAsync(string sourceId, CancellationToken ct) => throw new NotImplementedException();
    }
}
