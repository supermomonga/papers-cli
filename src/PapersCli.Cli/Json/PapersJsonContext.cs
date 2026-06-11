using System.Text.Json;
using System.Text.Json.Serialization;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Json;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(Paper))]
[JsonSerializable(typeof(Paper[]))]
[JsonSerializable(typeof(List<Paper>))]
[JsonSerializable(typeof(IReadOnlyList<Paper>))]
[JsonSerializable(typeof(PaperFile))]
[JsonSerializable(typeof(PaperFile[]))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchResult[]))]
[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(IReadOnlyList<SearchResult>))]
[JsonSerializable(typeof(SearchResultsPage))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(DeleteResult))]
public partial class PapersJsonContext : JsonSerializerContext;

public record DeleteResult(string Deleted);
