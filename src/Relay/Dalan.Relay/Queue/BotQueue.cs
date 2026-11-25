namespace Dalan.Relay.Queue;

/// <summary>
/// The waiting updates of one bot, lowest id first. Pure logic: the caller gives the time and the settings.
/// The list is short (updates live about a minute), so plain scans are enough.
/// </summary>
#pragma warning disable CA1001 // The poll token source has no timer, so it holds nothing that needs Dispose.
public sealed class BotQueue
#pragma warning restore CA1001
{
    private sealed class Entry(QueuedUpdate update)
    {
        public QueuedUpdate Update { get; } = update;
        public bool HandedOut { get; set; }
    }

    private readonly Lock _lock = new();
    private readonly List<Entry> _entries = [];
    private TaskCompletionSource _arrival = NewSignal();
    private CancellationTokenSource _poll = new();

    public IReadOnlyList<long> Enqueue(QueuedUpdate update, bool latestCommandWins)
    {
        lock (_lock)
        {
            if (_entries.Exists(e => e.Update.Id == update.Id)) return [];

            // Q-04: only updates the bot has not seen may be replaced.
            // REL-56: a moved chat is news for the bot, never a command that replaces one.
            var replaced = latestCommandWins && update.Moved is null
                ? Remove(e => !e.HandedOut && e.Update.Moved is null && e.Update.From.Id == update.From.Id)
                : [];

            // Ids almost always arrive in order; when two messages race, keep the list sorted.
            var index = _entries.FindLastIndex(e => e.Update.Id < update.Id) + 1;
            _entries.Insert(index, new Entry(update));

            var arrival = _arrival;
            _arrival = NewSignal();
            arrival.SetResult();
            return replaced;
        }
    }

    public Batch Take(long offset, int limit, DateTimeOffset now, TimeSpan expiry)
    {
        lock (_lock)
        {
            // Q-03: an offset confirms only what was handed out. A late confirm still counts.
            var delivered = Remove(e => e.HandedOut && e.Update.Id < offset);
            // Q-02: after its expiry an update is not offered again, handed out or not.
            var notDelivered = Remove(e => IsExpired(e, now, expiry));

            var updates = _entries.Take(limit).ToList();
            foreach (var entry in updates) entry.HandedOut = true;

            return new Batch(updates.ConvertAll(e => e.Update), delivered, notDelivered, _arrival.Task);
        }
    }

    public IReadOnlyList<long> RemoveExpired(DateTimeOffset now, TimeSpan expiry)
    {
        lock (_lock)
        {
            // A handed-out update stays, so a slow bot can still confirm it.
            return Remove(e => !e.HandedOut && IsExpired(e, now, expiry));
        }
    }

    public CancellationToken BeginPoll()
    {
        lock (_lock)
        {
            _poll.Cancel();
            _poll = new CancellationTokenSource();
            return _poll.Token;
        }
    }

    private static bool IsExpired(Entry entry, DateTimeOffset now, TimeSpan expiry) => entry.Update.Date + expiry <= now;

    private List<long> Remove(Predicate<Entry> match)
    {
        var ids = _entries.FindAll(match).ConvertAll(e => e.Update.Id);
        _entries.RemoveAll(match);
        return ids;
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
