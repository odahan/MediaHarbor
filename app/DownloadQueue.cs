using System.Diagnostics;

namespace MediaHarbor;

/// <summary>Grants slots on the owning UI thread, pacing playlist starts across all workers.</summary>
internal sealed class DownloadQueue(int limit, Func<int, TimeSpan>? spacing = null)
{
    private readonly Queue<TaskCompletionSource<string?>> waiting = new();
    private readonly Queue<TaskCompletionSource<string?>> playlist = new();
    private int active;
    private long lastStart;
    private TimeSpan startInterval;
    private DateTimeOffset retryAfter;
    private string? pauseReason;
    private bool wakeScheduled;

    internal int Limit { get; private set; } = limit;
    internal string? PauseReason => pauseReason;

    internal void SetLimit(int value)
    {
        Limit = Math.Clamp(value, 1, 8);
        Admit();
    }

    /// <summary>Returns a pause reason when the request was stopped without acquiring a slot.</summary>
    internal async Task<string?> WaitAsync(CancellationToken token = default, bool playlistEntry = false)
    {
        token.ThrowIfCancellationRequested();
        if (pauseReason is not null) return pauseReason;
        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => request.TrySetCanceled(token));
        (playlistEntry ? playlist : waiting).Enqueue(request);
        Admit();
        return await request.Task;
    }

    internal void Release()
    {
        if (active >= 8 && playlist.Count > 0) PaceNextStart();
        active--;
        Admit();
    }

    internal void Pause(string reason)
    {
        pauseReason = reason;
        while (waiting.TryDequeue(out var request)) request.TrySetResult(reason);
        while (playlist.TryDequeue(out var request)) request.TrySetResult(reason);
    }

    internal void Resume() => pauseReason = null;

    internal bool DeferUntil(DateTimeOffset until)
    {
        if (until <= retryAfter || until <= DateTimeOffset.UtcNow) return false;
        retryAfter = until;
        return true;
    }

    private void Admit()
    {
        while (active < Limit)
        {
            var queue = waiting.Count > 0 ? waiting : playlist;
            if (!queue.TryPeek(out var request)) return;
            if (request.Task.IsCompleted)
            {
                queue.Dequeue();
                continue;
            }

            var intervalRemaining = queue == playlist ? startInterval - Stopwatch.GetElapsedTime(lastStart) : TimeSpan.Zero;
            var retryRemaining = retryAfter - DateTimeOffset.UtcNow;
            var delay = intervalRemaining > retryRemaining ? intervalRemaining : retryRemaining;
            if (delay > TimeSpan.Zero)
            {
                if (!wakeScheduled) _ = WakeAsync(delay);
                return;
            }

            queue.Dequeue();
            if (!request.TrySetResult(null)) continue;
            active++;
            if (queue == playlist) PaceNextStart();
        }
    }

    private void PaceNextStart()
    {
        lastStart = Stopwatch.GetTimestamp();
        startInterval = spacing?.Invoke(active) ?? StartInterval(active);
    }

    private async Task WakeAsync(TimeSpan delay)
    {
        wakeScheduled = true;
        await Task.Delay(delay > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay);
        wakeScheduled = false;
        Admit();
    }

    internal static TimeSpan StartInterval(int active) => TimeSpan.FromMilliseconds(active >= 8
        ? Random.Shared.Next(2001, 4001)
        : Random.Shared.Next(500, 2001));
}
