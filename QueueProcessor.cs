﻿using Microsoft.EntityFrameworkCore;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class QueueProcessor : IHostedService
{
    private readonly ItemQueue itemQueue;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<QueueProcessor> logger;

    private readonly CancellationTokenSource cts;

    private Task? queueRunnerTask;

    public QueueProcessor(
        ItemQueue itemQueue,
        IServiceProvider serviceProvider,
        ILogger<QueueProcessor> logger
    )
    {
        this.itemQueue = itemQueue;
        this.serviceProvider = serviceProvider;
        this.logger = logger;

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
            logger.LogInformation("Looking for items");

            List<KeyValuePair<int, List<ProcessPersonalBestRequest>>[]> chunks = itemQueue.GetItemsFromQueue()
                .Chunk(10).ToList();

            logger.LogTrace("Got {Count} items", chunks.Count);
            
            foreach (KeyValuePair<int, List<ProcessPersonalBestRequest>>[] chunk in chunks)
            {
                List<IServiceScope> scopes = new();
                List<Task> tasks = new();

                logger.LogTrace("Processing chunk");
                
                foreach (KeyValuePair<int, List<ProcessPersonalBestRequest>> kvp in chunk)
                {
                    logger.LogTrace("Processing user {User}", kvp.Key);
                    
                    IServiceScope scope = serviceProvider.CreateScope();
                    scopes.Add(scope);
                    Task task = ProcessQueue(scope.ServiceProvider,
                        kvp.Value,
                        CancellationToken.None); // TODO: Check if we should give a different CT here
                    tasks.Add(task);
                }

                logger.LogTrace("Waiting for all tasks");
                await Task.WhenAll(tasks);

                logger.LogTrace("Cleaning up scopes");
                foreach (IServiceScope scope in scopes)
                {
                    scope.Dispose();
                }
            }

            await Task.Delay(1000, ct);
        }
    }

    private async Task ProcessQueue(
        IServiceProvider provider,
        List<ProcessPersonalBestRequest> requests,
        CancellationToken ct
    )
    {
        logger.LogTrace("Getting context");
        GTRContext context = provider.GetRequiredService<GTRContext>();

        foreach (ProcessPersonalBestRequest request in requests)
        {
            logger.LogTrace("Getting personal bests");
            List<Record> personalBests = await context.Records
                .Where(x => x.Level == request.Level && x.User == request.User && x.IsBest)
                .ToListAsync(ct);

            foreach (Record personalBest in personalBests)
            {
                logger.LogTrace("Setting personal best to not best");
                personalBest.IsBest = false;
            }

            logger.LogTrace("Get best record");
            Record? record = await context.Records
                .Where(x => x.Level == request.Level && x.User == request.User)
                .OrderBy(x => x.Time)
                .FirstOrDefaultAsync(ct);

            if (record == null)
            {
                logger.LogError("Unable to mark record as best because it does not exist");
            }
            else
            {
                logger.LogTrace("Marking record as best");
                record.IsBest = true;
            }

            logger.LogTrace("Saving changes");
            await context.SaveChangesAsync(ct);
        }
    }
}