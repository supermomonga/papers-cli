using System.Net;
using System.Text;
using PapersCli.Cli.Sources;

namespace PapersCli.Cli.Tests.Sources;

public class SearchSourceTests
{
    [Test]
    public async Task ArxivSearch_ParsesTotalResultsAndUsesZeroBasedStart()
    {
        var handler = new StubHttpMessageHandler("""
            <?xml version='1.0' encoding='UTF-8'?>
            <feed xmlns:opensearch="http://a9.com/-/spec/opensearch/1.1/" xmlns:arxiv="http://arxiv.org/schemas/atom" xmlns="http://www.w3.org/2005/Atom">
              <opensearch:itemsPerPage>10</opensearch:itemsPerPage>
              <opensearch:totalResults>42</opensearch:totalResults>
              <opensearch:startIndex>10</opensearch:startIndex>
              <entry>
                <id>http://arxiv.org/abs/2301.00001v1</id>
                <title>Attention Test</title>
                <summary>Abstract</summary>
                <published>2023-01-02T00:00:00Z</published>
                <author><name>Alice</name></author>
                <category term="cs.AI"/>
                <link href="https://arxiv.org/pdf/2301.00001v1" title="pdf"/>
              </entry>
            </feed>
            """);
        var source = new ArxivSource(new HttpClient(handler));

        var page = await source.SearchAsync("attention", fromYear: 2023, toYear: 2023, sort: "date", limit: 10, page: 2);

        await Assert.That(page.TotalResults).IsEqualTo(42);
        await Assert.That(page.Page).IsEqualTo(2);
        await Assert.That(page.Limit).IsEqualTo(10);
        await Assert.That(page.Results.Count).IsEqualTo(1);
        await Assert.That(page.Results[0].SourceId).IsEqualTo("2301.00001v1");
        await Assert.That(handler.LastRequestUri!.Query.Contains("start=10")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("max_results=10")).IsTrue();
        await Assert.That(Uri.UnescapeDataString(handler.LastRequestUri.Query).Contains("submittedDate")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("sortBy=submittedDate")).IsTrue();
    }

    [Test]
    public async Task CiniiSearch_ParsesTotalResultsAndUsesOneBasedStart()
    {
        var handler = new StubHttpMessageHandler("""
            {
              "opensearch:totalResults": 37,
              "opensearch:startIndex": 11,
              "opensearch:itemsPerPage": 10,
              "items": [
                {
                  "@id": "https://cir.nii.ac.jp/crid/1234567890",
                  "title": "CiNii Paper",
                  "dc:creator": ["Alice"],
                  "prism:publicationDate": "2024-01-01"
                }
              ]
            }
            """, "application/json");
        var source = new CiNiiSource(new HttpClient(handler));

        var page = await source.SearchAsync("deep learning", sort: "date", limit: 10, page: 2);

        await Assert.That(page.TotalResults).IsEqualTo(37);
        await Assert.That(page.Page).IsEqualTo(2);
        await Assert.That(page.Results.Count).IsEqualTo(1);
        await Assert.That(page.Results[0].Source).IsEqualTo("cinii");
        await Assert.That(page.Results[0].SourceId).IsEqualTo("1234567890");
        await Assert.That(handler.LastRequestUri!.Query.Contains("start=11")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("count=10")).IsTrue();
    }

    [Test]
    public async Task IrdbSearch_ReSourcesCiniiResultsAndUsesIrdbFilter()
    {
        var handler = new StubHttpMessageHandler("""
            {
              "opensearch:totalResults": 12,
              "opensearch:startIndex": 11,
              "opensearch:itemsPerPage": 10,
              "items": [
                {
                  "@id": "https://cir.nii.ac.jp/crid/9876543210",
                  "title": "IRDB Paper",
                  "dc:creator": ["Alice"]
                }
              ]
            }
            """, "application/json");
        var cinii = new CiNiiSource(new HttpClient(handler));
        var source = new IrdbSource(new HttpClient(handler), cinii);

        var page = await source.SearchAsync("repository", limit: 10, page: 2);

        await Assert.That(page.Source).IsEqualTo("irdb");
        await Assert.That(page.TotalResults).IsEqualTo(12);
        await Assert.That(page.Results.Count).IsEqualTo(1);
        await Assert.That(page.Results[0].Source).IsEqualTo("irdb");
        await Assert.That(handler.LastRequestUri!.Query.Contains("start=11")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("dataSourceType=IRDB")).IsTrue();
    }

    [Test]
    public async Task JStageSearch_ParsesTotalResultsAndKeepsWarnEntries()
    {
        var handler = new StubHttpMessageHandler("""
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom" xmlns:prism="http://prismstandard.org/namespaces/basic/2.0/" xmlns:opensearch="http://a9.com/-/spec/opensearch/1.1/">
              <result>
                <status>WARN_002</status>
                <message>WARN_002</message>
              </result>
              <opensearch:totalResults>1928</opensearch:totalResults>
              <opensearch:startIndex>11</opensearch:startIndex>
              <opensearch:itemsPerPage>10</opensearch:itemsPerPage>
              <entry>
                <article_title><ja><![CDATA[J-STAGE Paper]]></ja></article_title>
                <article_link><ja>https://www.jstage.jst.go.jp/article/journal/1/1/1/_article/-char/ja/</ja></article_link>
                <author><ja><name><![CDATA[Alice]]></name></ja></author>
                <material_title><ja><![CDATA[Test Journal]]></ja></material_title>
                <pubyear>2024</pubyear>
                <prism:doi>10.1234/jstage</prism:doi>
              </entry>
            </feed>
            """);
        var source = new JStageSource(new HttpClient(handler), new CiNiiSource(new HttpClient(handler)));

        var page = await source.SearchAsync("deep learning", sort: "title", limit: 10, page: 2);

        await Assert.That(page.TotalResults).IsEqualTo(1928);
        await Assert.That(page.Page).IsEqualTo(2);
        await Assert.That(page.Results.Count).IsEqualTo(1);
        await Assert.That(page.Results[0].SourceId).IsEqualTo("10.1234/jstage");
        await Assert.That(handler.LastRequestUri!.Query.Contains("start=11")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("count=10")).IsTrue();
        await Assert.That(handler.LastRequestUri.Query.Contains("sortflg=5")).IsTrue();
    }

    private sealed class StubHttpMessageHandler(string response, string mediaType = "application/xml") : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, mediaType),
            });
        }
    }
}
