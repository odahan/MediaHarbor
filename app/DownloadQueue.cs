namespace MediaHarbor;

/// <summary>Grants download slots in arrival order on the owning UI thread.</summary>
internal sealed class DownloadQueue(int limit)
{
    private readonly Queue<TaskCompletionSource> waiting = new();
    private int active;

    internal int Limit { get; private set; } = limit;

    internal void SetLimit(int value)
    {
        Limit = Math.Clamp(value, 1, 8);
        Admit();
    }

    internal async Task WaitAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var request = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => request.TrySetCanceled(token));
        waiting.Enqueue(request);
        Admit();
        await request.Task;
    }

    internal void Release()
    {
        active--;
        Admit();
    }

    private void Admit()
    {
        while (active < Limit && waiting.TryDequeue(out var request))
        {
            if (request.TrySetResult()) active++;
        }
    }
}
