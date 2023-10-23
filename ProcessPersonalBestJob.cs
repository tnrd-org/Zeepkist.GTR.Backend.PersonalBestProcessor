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
        Record? bestTimeForLevelAndUser = await context.Records.AsNoTracking()
            .Where(x => x.Level == request.Level && x.IsValid && x.User == request.User)
            .OrderBy(x => x.Time)
            .FirstOrDefaultAsync();

        if (bestTimeForLevelAndUser == null)
        {
            logger.LogWarning("No best time found for level {Level} and user {User}", request.Level, request.User);
            return true;
        }

        Record newPersonalBest = bestTimeForLevelAndUser!;

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
                Level = newPersonalBest.Level,
                User = newPersonalBest.User,
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
