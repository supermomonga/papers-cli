namespace PapersCli.Cli.Sources;

public static class PaperIdParser
{
    public static (string Source, string SourceId) Parse(string input, IEnumerable<IPaperSource> sources)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Empty paper identifier.");

        // URL format
        if (input.StartsWith("http://") || input.StartsWith("https://"))
        {
            foreach (var source in sources)
            {
                var sourceId = source.ParseUrl(input);
                if (sourceId is not null)
                    return (source.Name, sourceId);
            }
            throw new ArgumentException($"Unrecognized URL: {input}");
        }

        // source:id format
        var colonIndex = input.IndexOf(':');
        if (colonIndex > 0 && colonIndex < input.Length - 1)
        {
            var sourceName = input[..colonIndex];
            var id = input[(colonIndex + 1)..];
            return (sourceName, id);
        }

        throw new ArgumentException($"Invalid paper identifier: {input}. Use 'source:id' format or a URL.");
    }
}
