using ConsoleAppFramework;
using PapersCli.Cli.Config;
using Spectre.Console;

namespace PapersCli.Cli.Commands;

public class ConfigCommand(AppConfig config)
{
    /// <summary>
    /// Initialize config file with defaults.
    /// </summary>
    [Command("init")]
    public void Init()
    {
        if (File.Exists(AppConfig.ConfigFilePath))
        {
            AnsiConsole.MarkupLine($"[yellow]Config file already exists: {Markup.Escape(AppConfig.ConfigFilePath)}[/]");
            return;
        }

        config.Save();
        AnsiConsole.MarkupLine($"[green]Config file created: {Markup.Escape(AppConfig.ConfigFilePath)}[/]");
    }

    /// <summary>
    /// Show all config values.
    /// </summary>
    [Command("show")]
    public void Show()
    {
        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Value");

        table.AddRow("download-dir", Markup.Escape(config.DownloadDir));
        table.AddRow("default-source", Markup.Escape(config.DefaultSource));

        foreach (var (key, value) in config.ApiKeys)
        {
            table.AddRow($"api-keys.{Markup.Escape(key)}", Markup.Escape(value));
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Get a config value.
    /// </summary>
    /// <param name="key">Config key (e.g. download-dir, default-source, api-keys.xxx).</param>
    [Command("get")]
    public void Get([Argument] string key)
    {
        var value = config.GetValue(key);
        if (value is null)
        {
            Console.Error.WriteLine($"Unknown config key: {key}");
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine(value);
    }

    /// <summary>
    /// Set a config value.
    /// </summary>
    /// <param name="key">Config key (e.g. download-dir, default-source, api-keys.xxx).</param>
    /// <param name="value">Value to set.</param>
    [Command("set")]
    public void Set([Argument] string key, [Argument] string value)
    {
        if (!config.SetValue(key, value))
        {
            Console.Error.WriteLine($"Unknown config key: {key}");
            Environment.ExitCode = 1;
            return;
        }

        config.Save();
        AnsiConsole.MarkupLine($"[green]Set {Markup.Escape(key)} = {Markup.Escape(value)}[/]");
    }
}
