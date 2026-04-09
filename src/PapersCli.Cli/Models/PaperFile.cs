namespace PapersCli.Cli.Models;

public record PaperFile
{
    public long Id { get; init; }
    public long PaperId { get; init; }
    public required string Format { get; init; }
    public required string FilePath { get; init; }
    public required string SourceUrl { get; init; }
    public required string DownloadedAt { get; init; }
}
