using FluentResults;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class ItemQueue
{
    private readonly AutoResetEvent resetEvent = new(true);
    private readonly Dictionary<int, List<ProcessPersonalBestRequest>> userToItems = new();

    public bool HasItems()
    {
        return userToItems.Values.Any(x => x.Count > 0);
    }

    public void AddToQueue(ProcessPersonalBestRequest item)
    {
        resetEvent.WaitOne();

        if (!userToItems.ContainsKey(item.User))
            userToItems.Add(item.User, new List<ProcessPersonalBestRequest>());

        userToItems[item.Level].Add(item);

        resetEvent.Set();
    }

    public Dictionary<int, List<ProcessPersonalBestRequest>> GetItemsFromQueue()
    {
        resetEvent.WaitOne();

        Dictionary<int, List<ProcessPersonalBestRequest>> copy = userToItems.ToDictionary(x => x.Key, y => y.Value);
        userToItems.Clear();

        resetEvent.Set();
        return copy;
    }
}
