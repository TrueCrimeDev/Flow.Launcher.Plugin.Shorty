using Shorty.Models;
using Shorty.Services;
using Xunit;

namespace Shorty.Tests;

public sealed class AnswerCacheTests
{
    [Fact]
    public void AddEvictsLeastRecentlyUsedEntryWhenCapacityIsExceeded()
    {
        var cache = new AnswerCache(2);
        var first = Entry("aaaa1111", "first");
        var second = Entry("bbbb2222", "second");
        var third = Entry("cccc3333", "third");

        cache.Set(first);
        cache.Set(second);
        Assert.True(cache.TryGet("aaaa1111", out _));
        cache.Set(third);

        Assert.True(cache.TryGet("aaaa1111", out _));
        Assert.False(cache.TryGet("bbbb2222", out _));
        Assert.True(cache.TryGet("cccc3333", out _));
    }

    [Fact]
    public void EvictedEntryLeavesRequestMetadataForAskAgain()
    {
        var cache = new AnswerCache(1);
        cache.Set(Entry("aaaa1111", "first"));
        cache.Set(Entry("bbbb2222", "second"));

        var found = cache.TryGetExpiredRequest("aaaa1111", out var request);

        Assert.True(found);
        Assert.Equal("default", request.Preset);
        Assert.Equal("first", request.Question);
    }

    private static AnswerEntry Entry(string id, string question)
    {
        return new AnswerEntry
        {
            Id = id,
            Question = question,
            Text = $"answer for {question}",
            Preset = "default",
            Model = "gpt-test",
            Created = DateTimeOffset.UtcNow
        };
    }
}
