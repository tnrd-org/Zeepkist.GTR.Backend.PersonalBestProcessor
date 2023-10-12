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
            if (itemQueue.HasItems())
            {
                await ProcessQueue(stoppingToken);
            }
            else
            {
                logger.LogInformation("No items in queue");
            }

            logger.LogInformation("Delaying for 1 second");
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessQueue(CancellationToken stoppingToken)
    {
        List<ProcessPersonalBestRequest> items = itemQueue.GetItemsFromQueue();
        List<Task> tasks = new();

        List<IGrouping<int, ProcessPersonalBestRequest>> groupedByUser = items.GroupBy(x => x.User).ToList();
        foreach (IGrouping<int, ProcessPersonalBestRequest> userGroup in groupedByUser)
        {
            List<IGrouping<int, ProcessPersonalBestRequest>> groupedByLevel =
                userGroup.GroupBy(x => x.Level).ToList();

            foreach (IGrouping<int, ProcessPersonalBestRequest> levelGroup in groupedByLevel)
            {
                // We only need to process one request since we're checking the database for the best time
                ProcessPersonalBestRequest request = levelGroup.First();
                Task task = Task.Run(async () => { await StartJob(request, stoppingToken); }, stoppingToken);
                tasks.Add(task);
            }
        }

        await Task.WhenAll(tasks);
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
