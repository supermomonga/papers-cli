using PapersCli.Cli.Models;

namespace PapersCli.Cli.Tests.Models;

public class PaperTests
{
    [Test]
    public async Task GetAuthorsList_ValidJson_ReturnsAuthors()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[\"Alice\",\"Bob\"]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
        };

        var authors = paper.GetAuthorsList();
        await Assert.That(authors.Count).IsEqualTo(2);
        await Assert.That(authors[0]).IsEqualTo("Alice");
        await Assert.That(authors[1]).IsEqualTo("Bob");
    }

    [Test]
    public async Task GetCategoriesList_ValidJson_ReturnsCategories()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
            Categories = "[\"cs.AI\",\"cs.CL\"]",
        };

        var cats = paper.GetCategoriesList();
        await Assert.That(cats.Count).IsEqualTo(2);
        await Assert.That(cats[0]).IsEqualTo("cs.AI");
    }

    [Test]
    public async Task GetCategoriesList_NullCategories_ReturnsEmpty()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
            Categories = null,
        };

        var cats = paper.GetCategoriesList();
        await Assert.That(cats.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisplayId_FormatsCorrectly()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
        };

        await Assert.That(paper.DisplayId).IsEqualTo("arxiv:2301.00001");
    }

    [Test]
    public async Task PublishedYear_ValidDate_ReturnsYear()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
            PublishedAt = "2023-07-15T00:00:00Z",
        };

        await Assert.That(paper.PublishedYear).IsEqualTo(2023);
    }

    [Test]
    public async Task PublishedYear_NullDate_ReturnsNull()
    {
        var paper = new Paper
        {
            Source = "arxiv", SourceId = "2301.00001",
            Title = "Test", Authors = "[]",
            Url = "https://example.com", CreatedAt = "2024-01-01",
            PublishedAt = null,
        };

        await Assert.That(paper.PublishedYear).IsNull();
    }
}
