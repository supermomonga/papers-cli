using PapersCli.Cli.Config;

namespace PapersCli.Cli.Tests.Config;

public class AppConfigTests
{
    [Test]
    [NotInParallel("Environment")]
    public async Task Load_NoConfigFile_ReturnsDefaults()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"papers-config-test-{Guid.NewGuid()}");
        var previousConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmpDir);
        try
        {
            var config = AppConfig.Load();
            await Assert.That(config.DownloadDir).IsEqualTo("~/papers");
            await Assert.That(config.DefaultSource).IsEqualTo("arxiv");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousConfigHome);
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }

    [Test]
    [NotInParallel("Environment")]
    public async Task SaveAndLoad_RoundTrip()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"papers-config-test-{Guid.NewGuid()}");
        var previousConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmpDir);
        try
        {
            var config = new AppConfig
            {
                DownloadDir = "/tmp/my-papers",
                DefaultSource = "jstage",
            };
            config.Save();

            var loaded = AppConfig.Load();
            await Assert.That(loaded.DownloadDir).IsEqualTo("/tmp/my-papers");
            await Assert.That(loaded.DefaultSource).IsEqualTo("jstage");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", previousConfigHome);
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, true);
        }
    }

    [Test]
    public async Task GetValue_KnownKeys_ReturnsValues()
    {
        var config = new AppConfig { DownloadDir = "/papers", DefaultSource = "cinii" };

        await Assert.That(config.GetValue("download-dir")).IsEqualTo("/papers");
        await Assert.That(config.GetValue("default-source")).IsEqualTo("cinii");
    }

    [Test]
    public async Task GetValue_UnknownKey_ReturnsNull()
    {
        var config = new AppConfig();
        await Assert.That(config.GetValue("nonexistent")).IsNull();
    }

    [Test]
    public async Task SetValue_KnownKeys_UpdatesValues()
    {
        var config = new AppConfig();

        var result = config.SetValue("download-dir", "/new/path");
        await Assert.That(result).IsTrue();
        await Assert.That(config.DownloadDir).IsEqualTo("/new/path");
    }

    [Test]
    public async Task SetValue_UnknownKey_ReturnsFalse()
    {
        var config = new AppConfig();
        var result = config.SetValue("nonexistent", "value");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SetValue_ApiKey_SetsCorrectly()
    {
        var config = new AppConfig();

        var result = config.SetValue("api-keys.semantic-scholar", "abc123");
        await Assert.That(result).IsTrue();
        await Assert.That(config.ApiKeys["semantic-scholar"]).IsEqualTo("abc123");
        await Assert.That(config.GetValue("api-keys.semantic-scholar")).IsEqualTo("abc123");
    }

    [Test]
    public async Task ResolvedDownloadDir_ExpandsTilde()
    {
        var config = new AppConfig { DownloadDir = "~/papers" };
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        await Assert.That(config.ResolvedDownloadDir).IsEqualTo(Path.Combine(home, "papers"));
    }
}
