using PapersCli.Cli.Commands;

namespace PapersCli.Cli.Tests.Commands;

public class SearchResultInputParserTests
{
    [Test]
    public async Task ParseIds_ReadsLegacySearchResultArray()
    {
        var ids = SearchResultInputParser.ParseIds("""
            [
              {
                "source": "arxiv",
                "source_id": "2301.00001",
                "title": "Paper",
                "authors": "[]",
                "url": "https://arxiv.org/abs/2301.00001"
              }
            ]
            """);

        await Assert.That(ids.Count).IsEqualTo(1);
        await Assert.That(ids[0]).IsEqualTo("arxiv:2301.00001");
    }

    [Test]
    public async Task ParseIds_ReadsSearchResultsPageObject()
    {
        var ids = SearchResultInputParser.ParseIds("""
            {
              "source": "arxiv",
              "query": "attention",
              "page": 1,
              "limit": 20,
              "returned_results": 1,
              "total_results": 42,
              "total_pages": 3,
              "has_more": true,
              "results": [
                {
                  "source": "arxiv",
                  "source_id": "2301.00001",
                  "title": "Paper",
                  "authors": "[]",
                  "url": "https://arxiv.org/abs/2301.00001"
                }
              ]
            }
            """);

        await Assert.That(ids.Count).IsEqualTo(1);
        await Assert.That(ids[0]).IsEqualTo("arxiv:2301.00001");
    }

    [Test]
    public async Task ParseIds_FallsBackToLineSeparatedIds()
    {
        var ids = SearchResultInputParser.ParseIds("""
            arxiv:2301.00001
            jstage:10.1234/test
            """);

        await Assert.That(ids.Count).IsEqualTo(2);
        await Assert.That(ids[0]).IsEqualTo("arxiv:2301.00001");
        await Assert.That(ids[1]).IsEqualTo("jstage:10.1234/test");
    }
}
