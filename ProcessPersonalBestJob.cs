using Microsoft.EntityFrameworkCore;
using TNRD.Zeepkist.GTR.Database;
using TNRD.Zeepkist.GTR.Database.Models;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

public class ProcessPersonalBestJob
{
    private readonly ILogger<ProcessPersonalBestJob> logger;
    private readonly GTRContext context;
    private readonly ProcessPersonalBestRequest request;

    public ProcessPersonalBestJob(
        ILogger<ProcessPersonalBestJob> logger,
        GTRContext context,
        ProcessPersonalBestRequest request
    )
    {
        this.logger = logger;
        this.context = context;
        this.request = request;
    }

    public async Task<bool> Execute()
    {
        List<Record> bestTimesForLevelAndUser = await context.Records.AsNoTracking()
            .Where(x => x.Level == request.Level && x.IsValid && x.User == request.User)
            .OrderBy(x => x.Time)
            .Take(1)
            .ToListAsync();

        if (bestTimesForLevelAndUser.Count != 1)
        {
            logger.LogCritical("Found {Amount} instead of 1 record for level {Level} and user {User}",
                bestTimesForLevelAndUser.Count,
                request.Level,
                request.User);

            return false;
        }

        Record newPersonalBest = bestTimesForLevelAndUser.First();

        PersonalBest? existingPersonalBest =
            await context.PersonalBests.FirstOrDefaultAsync(x => x.Level == request.Level && x.User == request.User);

        if (existingPersonalBest != null)
        {
            existingPersonalBest.Record = newPersonalBest.Id;
        }
        else
        {
            context.PersonalBests.Add(new PersonalBest()
            {
                Level = request.Level,
                User = request.User,
                Record = newPersonalBest.Id,
                DateCreated = DateTime.UtcNow
            });
        }

        int savedChanges = await context.SaveChangesAsync();

        if (savedChanges != 1)
        {
            logger.LogWarning("No saved changes when processing personal best for user {User} on level {Level}",
                request.User,
                request.Level);
        }

        return true;
    }
}
