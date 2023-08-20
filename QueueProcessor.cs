using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class QueueProcessor : IHostedService
{
    private readonly ItemQueue itemQueue;
    private readonly ILogger<QueueProcessor> logger;
    private readonly GTRContext context;
    private readonly CancellationTokenSource cts;

    private Task? queueRunnerTask;

    public QueueProcessor(
        ItemQueue itemQueue,
        ILogger<QueueProcessor> logger,
        GTRContext context
    )
    {
        this.itemQueue = itemQueue;
        this.logger = logger;
        this.context = context;

        cts = new CancellationTokenSource();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        queueRunnerTask = QueueRunner(cts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        cts.Cancel();

        if (queueRunnerTask != null)
        {
            await queueRunnerTask;
        }

        cts.Dispose();
    }

    private async Task? QueueRunner(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            List<KeyValuePair<int, List<ProcessPersonalBestRequest>>> items = itemQueue
                .GetItemsFromQueue()
                .ToList();

            foreach (KeyValuePair<int, List<ProcessPersonalBestRequest>> item in items)
            {
                int userId = item.Key;
                List<IGrouping<int, ProcessPersonalBestRequest>> groups = item.Value
                    .GroupBy(x => x.Level)
                    .ToList();

                foreach (IGrouping<int, ProcessPersonalBestRequest> group in groups)
                {
                    int levelId = group.Key;
                    await ProcessRequest(userId, levelId);
                }
            }

            if (!itemQueue.HasItems())
            {
                logger.LogInformation("Delaying queue runner for 1 second");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessRequest(int userId, int levelId)
    {
        try
        {
            await context.UpdatePersonalBestAsync(userId, levelId);
        }
        catch (Exception e)
        {
            logger.LogError(e,
                "Failed to update personal best for user {UserId} and level {LevelId}",
                userId,
                levelId);
        }
    }
}
