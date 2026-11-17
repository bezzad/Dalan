using Dalan.Identity.Contracts;
using Dalan.Relay.Contracts;
using Dalan.Relay.Queue;

namespace Dalan.Relay.Tests;

/// <summary>The queue rules as pure logic: no database, no clock.</summary>
public class BotQueueTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Expiry = TimeSpan.FromSeconds(60);
    private static readonly DateTimeOffset BeforeExpiry = Start.AddSeconds(59);
    private static readonly DateTimeOffset AfterExpiry = Start.AddSeconds(60);

    private readonly BotQueue _queue = new();

    private static QueuedUpdate Update(long id, long userId = 1) =>
        new(id, new UserProfile(userId, "Sara", null), Start, new BotMessage(id, userId, FromBot: false, $"text {id}", Start));

    private IReadOnlyList<long> Add(long id, long userId = 1, bool latestCommandWins = false) =>
        _queue.Enqueue(Update(id, userId), latestCommandWins);

    private Batch Take(long offset = 0, int limit = 100, DateTimeOffset? now = null) =>
        _queue.Take(offset, limit, now ?? Start, Expiry);

    private static long[] Ids(Batch batch) => [.. batch.Updates.Select(u => u.Id)];

    [Fact]
    [Trait("Rule", "Q-02")]
    public void Q02_update_is_returned_before_its_expiry()
    {
        Add(5);

        Assert.Equal([5], Ids(Take(now: BeforeExpiry)));
    }

    [Fact]
    [Trait("Rule", "Q-02")]
    public void Q02_update_never_handed_out_is_dropped_at_its_expiry()
    {
        Add(5);

        var batch = Take(now: AfterExpiry);

        Assert.Empty(batch.Updates);
        Assert.Equal([5], batch.NotDelivered);
    }

    [Fact]
    [Trait("Rule", "Q-02")]
    public void Q02_sweep_removes_only_expired_updates_never_handed_out()
    {
        Add(5);
        Take();
        Add(6);

        Assert.Empty(_queue.RemoveExpired(BeforeExpiry, Expiry));
        Assert.Equal([6], _queue.RemoveExpired(AfterExpiry, Expiry));
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public void Q03_update_is_returned_again_until_it_is_confirmed()
    {
        Add(5);
        Take();

        var again = Take(offset: 5);

        Assert.Equal([5], Ids(again));
        Assert.Empty(again.Delivered);
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public void Q03_bigger_offset_confirms_the_update()
    {
        Add(5);
        Take();

        var next = Take(offset: 6);

        Assert.Empty(next.Updates);
        Assert.Equal([5], next.Delivered);
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public void Q03_confirm_after_the_expiry_still_counts()
    {
        Add(5);
        Take(now: BeforeExpiry);

        var next = Take(offset: 6, now: AfterExpiry);

        Assert.Equal([5], next.Delivered);
        Assert.Empty(next.NotDelivered);
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public void Q03_offset_does_not_confirm_an_update_the_bot_never_got()
    {
        Add(5);

        var batch = Take(offset: 9);

        Assert.Equal([5], Ids(batch));
        Assert.Empty(batch.Delivered);
    }

    [Fact]
    [Trait("Rule", "Q-03")]
    public void Q03_handed_out_but_not_confirmed_is_dropped_after_the_expiry()
    {
        Add(5);
        Take(now: BeforeExpiry);

        var again = Take(offset: 5, now: AfterExpiry);

        Assert.Empty(again.Updates);
        Assert.Equal([5], again.NotDelivered);
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public void Q04_latest_command_replaces_older_from_same_user()
    {
        Assert.Empty(Add(1, latestCommandWins: true));
        Assert.Equal([1], Add(2, latestCommandWins: true));
        Assert.Equal([2], Add(3, latestCommandWins: true));

        Assert.Equal([3], Ids(Take()));
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public void Q04_different_users_do_not_replace_each_other()
    {
        Add(1, userId: 1, latestCommandWins: true);
        Assert.Empty(Add(2, userId: 2, latestCommandWins: true));

        Assert.Equal([1, 2], Ids(Take()));
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public void Q04_update_already_handed_out_is_not_replaced()
    {
        Add(1, latestCommandWins: true);
        Take();

        Assert.Empty(Add(2, latestCommandWins: true));
        Assert.Equal([1, 2], Ids(Take()));
    }

    [Fact]
    [Trait("Rule", "Q-04")]
    public void Q04_setting_off_keeps_every_message()
    {
        Add(1);
        Add(2);
        Add(3);

        Assert.Equal([1, 2, 3], Ids(Take()));
    }

    [Fact]
    [Trait("Rule", "Q-08")]
    public void Q08_returns_lowest_ids_first_up_to_the_limit()
    {
        foreach (var id in new long[] { 4, 2, 5, 1, 3 }) Add(id);

        Assert.Equal([1, 2], Ids(Take(limit: 2)));
    }

    [Fact]
    [Trait("Rule", "Q-06")]
    public void Q06_arrival_signal_completes_when_an_update_is_added()
    {
        var arrival = Take().Arrival;
        Assert.False(arrival.IsCompleted);

        Add(1);

        Assert.True(arrival.IsCompleted);
    }

    [Fact]
    [Trait("Rule", "Q-05")]
    public void Q05_new_poll_cancels_the_one_before()
    {
        var first = _queue.BeginPoll();
        var second = _queue.BeginPoll();

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
    }
}
