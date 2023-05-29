using Serilog;
using TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;
using TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor.Rabbit;
using TNRD.Zeepkist.GTR.Database;

internal class Program
{
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .UseSerilog((context, configuration) =>
            {
                configuration
                    .MinimumLevel.Verbose()
                    .WriteTo.Console();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<QueueProcessor>();

                services.Configure<RabbitOptions>(context.Configuration.GetSection("Rabbit"));
                services.AddHostedService<RabbitWorker>();

                services.AddNpgsql<GTRContext>(context.Configuration["Database:ConnectionString"]);

                services.AddSingleton<ItemQueue>();
            })
            .Build();

        ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();

        TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
        {
            logger.LogError(eventArgs.Exception, "Unobserved task exception");
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            logger.LogError(eventArgs.ExceptionObject as Exception, "Unhandled exception");
        };

        host.Run();
    }
}
