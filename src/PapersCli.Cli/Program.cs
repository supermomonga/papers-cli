using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using PapersCli.Cli.Commands;
using PapersCli.Cli.Config;
using PapersCli.Cli.Data;
using PapersCli.Cli.Sources;

var app = ConsoleApp.Create()
    .ConfigureServices(services =>
    {
        var config = AppConfig.Load();
        services.AddSingleton(config);

        services.AddSingleton<HttpClient>();

        services.AddSingleton<PaperRepository>();

        // CiNii is an internal service used by JStageSource and IrdbSource (not user-facing)
        services.AddSingleton<CiNiiSource>();

        // User-facing sources
        services.AddSingleton<IPaperSource, ArxivSource>();
        services.AddSingleton<IPaperSource, JStageSource>();
        services.AddSingleton<IPaperSource, IrdbSource>();
    });

app.Add<PaperCommands>();
app.Add<ConfigCommand>("config");
app.Run(args);
