using Dalan.Relay.BotApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Dalan.Relay.Tests;

public class PollRequestTests
{
    private static readonly RelayOptions Options = new()
    {
        PollTimeoutDefaultSeconds = 30,
        PollTimeoutMaxSeconds = 50,
        MaxUpdatesPerPoll = 100,
    };

    private static PollRequest? Parse(string query) =>
        PollRequest.Parse(new QueryCollection(QueryHelpers.ParseQuery(query)), Options, out _);

    [Fact]
    [Trait("Rule", "Q-06")]
    public void Q06_no_timeout_means_the_default() =>
        Assert.Equal(TimeSpan.FromSeconds(30), Parse("")!.Timeout);

    [Fact]
    [Trait("Rule", "Q-06")]
    public void Q06_timeout_above_the_maximum_is_cut() =>
        Assert.Equal(TimeSpan.FromSeconds(50), Parse("timeout=600")!.Timeout);

    [Fact]
    [Trait("Rule", "Q-06")]
    public void Q06_timeout_inside_the_range_is_kept() =>
        Assert.Equal(TimeSpan.FromSeconds(7), Parse("timeout=7")!.Timeout);

    [Fact]
    [Trait("Rule", "Q-08")]
    public void Q08_no_limit_means_the_maximum() =>
        Assert.Equal(100, Parse("")!.Limit);

    [Fact]
    [Trait("Rule", "Q-08")]
    public void Q08_limit_above_the_maximum_is_cut() =>
        Assert.Equal(100, Parse("limit=5000")!.Limit);

    [Theory]
    [Trait("Rule", "REL-07")]
    [InlineData("limit=abc")]
    [InlineData("limit=0")]
    [InlineData("timeout=-1")]
    [InlineData("offset=1.5")]
    public void REL07_bad_parameter_is_refused(string query) =>
        Assert.Null(Parse(query));
}
