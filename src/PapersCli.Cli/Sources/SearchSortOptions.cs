namespace PapersCli.Cli.Sources;

public record SearchSortOption(string Key, IReadOnlyList<string> SupportedOrders, string DefaultOrder);

public static class SearchSortOptions
{
    public const string Asc = "asc";
    public const string Desc = "desc";

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    public static IReadOnlyList<SearchSortOption> ForSource(string sourceName) => sourceName switch
    {
        "arxiv" => [new("relevance", [Asc, Desc], Desc), new("date", [Asc, Desc], Desc)],
        "cinii" or "irdb" => [new("relevance", [Desc], Desc), new("date", [Desc], Desc)],
        "jstage" => [new("relevance", [Desc], Desc), new("date", [Desc], Desc), new("title", [Asc], Asc)],
        _ => [new("relevance", [Desc], Desc)],
    };

    public static SearchSortOption? Find(string sourceName, string sortKey)
        => ForSource(sourceName).FirstOrDefault(option => option.Key == sortKey);

    public static string ResolveOrder(SearchSortOption option, string? sortOrder)
        => string.IsNullOrWhiteSpace(sortOrder)
            ? option.DefaultOrder
            : Normalize(sortOrder);

    public static bool SupportsOrder(SearchSortOption option, string sortOrder)
        => option.SupportedOrders.Contains(sortOrder);

    public static string ResolveAndValidate(string sourceName, string sortKey, string? sortOrder)
    {
        var option = Find(sourceName, sortKey);
        if (option is null)
            throw new ArgumentException(FormatUnsupportedKey(sourceName, sortKey));

        var resolvedSortOrder = ResolveOrder(option, sortOrder);
        if (!SupportsOrder(option, resolvedSortOrder))
            throw new ArgumentException(FormatUnsupportedOrder(sourceName, sortKey, resolvedSortOrder));

        return resolvedSortOrder;
    }

    public static string FormatUnsupportedKey(string sourceName, string sortKey)
        => $"Sort '{sortKey}' is not supported by {sourceName}. Supported sort keys:{Environment.NewLine}{FormatSupportedKeys(sourceName)}";

    public static string FormatUnsupportedOrder(string sourceName, string sortKey, string sortOrder)
        => $"Sort order '{sortOrder}' is not supported for sort key '{sortKey}' by {sourceName}. Supported sort keys:{Environment.NewLine}{FormatSupportedKeys(sourceName)}";

    public static string FormatSupportedKeys(string sourceName)
        => string.Join(Environment.NewLine, ForSource(sourceName).Select(FormatOption));

    private static string FormatOption(SearchSortOption option)
        => $"- {option.Key} = {string.Join("|", option.SupportedOrders)} (default:{option.DefaultOrder})";
}
