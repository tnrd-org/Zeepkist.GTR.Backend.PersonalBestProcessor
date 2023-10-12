using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly ItemQueue itemQueue;
    private readonly IServiceProvider provider;
    private readonly SemaphoreSlim semaphore;

    public Worker(ILogger<Worker> logger, ItemQueue itemQueue, IServiceProvider provider)
    {
        this.logger = logger;
        this.itemQueue = itemQueue;
        this.provider = provider;

        semaphore = new SemaphoreSlim(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            List<ProcessPersonalBestRequest> items = itemQueue.GetItemsFromQueue();
            List<Task> tasks = new();

            foreach (ProcessPersonalBestRequest request in items)
            {
                Task task = Task.Run(async () => { await StartJob(request, stoppingToken); }, stoppingToken);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            if (!itemQueue.HasItems())
            {
                logger.LogInformation("No more items in queue, waiting for new items");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task StartJob(ProcessPersonalBestRequest request, CancellationToken stoppingToken)
    {
        await semaphore.WaitAsync(stoppingToken);

        IServiceScope? scope = null;

        try
        {
            scope = provider.CreateScope();

            ProcessPersonalBestJob job =
                ActivatorUtilities.CreateInstance<ProcessPersonalBestJob>(scope.ServiceProvider, request);

            bool success = await job.Execute();

            if (!success)
            {
                logger.LogError(
                    "Failed to process personal best for user {User} on level {Level}, queueing again",
                    request.User,
                    request.Level);

                itemQueue.AddToQueue(request);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while processing personal best");
        }
        finally
        {
            scope?.Dispose();
            semaphore.Release();
        }
    }
}
