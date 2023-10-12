using Serilog;
using TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor.Rabbit;
using TNRD.Zeepkist.GTR.Database;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class Program
{
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                configuration
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Source", "PersonalBestProcessor")
                    .MinimumLevel.Debug()
                    .WriteTo.Seq(context.Configuration["Seq:Url"], apiKey: context.Configuration["Seq:Key"])
                    .WriteTo.Console();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<Worker>();

                services.Configure<RabbitOptions>(context.Configuration.GetSection("Rabbit"));
                services.AddHostedService<RabbitWorker>();

                services.AddNpgsql<GTRContext>(context.Configuration["Database:ConnectionString"]);

                services.AddSingleton<ItemQueue>();
            })
            .Build();

        ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            logger.LogCritical(eventArgs.Exception, "Unobserved task exception");
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            logger.LogCritical(eventArgs.ExceptionObject as Exception, "Unhandled exception");
        };

        host.Run();
    }
}
