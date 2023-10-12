using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor;

internal class ItemQueue
{
    private readonly AutoResetEvent resetEvent = new(true);
    private readonly List<ProcessPersonalBestRequest> items = new();

    public bool HasItems()
    {
        resetEvent.WaitOne();

        try
        {
            return items.Count > 0;
        }
        finally
        {
            resetEvent.Set();
        }
    }

    public void AddToQueue(ProcessPersonalBestRequest item)
    {
        resetEvent.WaitOne();

        try
        {
            items.Add(item);
        }
        finally
        {
            resetEvent.Set();
        }
    }

    public List<ProcessPersonalBestRequest> GetItemsFromQueue()
    {
        resetEvent.WaitOne();

        try
        {
            List<ProcessPersonalBestRequest> copy = items.ToList();
            items.Clear();
            return copy;
        }
        finally
        {
            resetEvent.Set();
        }
    }
}
