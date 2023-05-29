using Serilog;
using TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;
using TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor.Rabbit;
using TNRD.Zeepkist.GTR.Database;

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

host.Run();
