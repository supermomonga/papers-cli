using CsToml;

namespace PapersCli.Cli.Config;

public class AppConfig
{
    public string DownloadDir { get; set; } = "~/papers";
    public string DefaultSource { get; set; } = "arxiv";
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    public static string ConfigDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "papers-cli");

    public static string ConfigFilePath => Path.Combine(ConfigDir, "config.toml");

    public static string DataDir => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
        "papers-cli");

    public static string DatabasePath => Path.Combine(DataDir, "papers.db");

    public string ResolvedDownloadDir => DownloadDir.Replace("~",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static AppConfig Load()
    {
        var config = new AppConfig();
        var path = ConfigFilePath;

        if (!File.Exists(path))
            return config;

        var bytes = File.ReadAllBytes(path);
        var doc = CsTomlSerializer.Deserialize<TomlDocument>(bytes);

        if (doc.RootNode["download-dir"u8].TryGetString(out var downloadDir))
            config.DownloadDir = downloadDir;
        if (doc.RootNode["default-source"u8].TryGetString(out var defaultSource))
            config.DefaultSource = defaultSource;

        // api-keys table is read by iterating known keys
        // For simplicity, we'll re-read the TOML when specific keys are needed

        return config;
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);

        using var writer = new StreamWriter(ConfigFilePath);
        writer.WriteLine($"download-dir = \"{DownloadDir}\"");
        writer.WriteLine($"default-source = \"{DefaultSource}\"");
        writer.WriteLine();
        writer.WriteLine("[api-keys]");
        foreach (var (key, value) in ApiKeys)
        {
            writer.WriteLine($"{key} = \"{value}\"");
        }
    }

    public string? GetValue(string key) => key switch
    {
        "download-dir" => DownloadDir,
        "default-source" => DefaultSource,
        _ when key.StartsWith("api-keys.") => ApiKeys.GetValueOrDefault(key["api-keys.".Length..]),
        _ => null,
    };

    public bool SetValue(string key, string value)
    {
        switch (key)
        {
            case "download-dir":
                DownloadDir = value;
                return true;
            case "default-source":
                DefaultSource = value;
                return true;
            default:
                if (key.StartsWith("api-keys."))
                {
                    ApiKeys[key["api-keys.".Length..]] = value;
                    return true;
                }
                return false;
        }
    }
}
