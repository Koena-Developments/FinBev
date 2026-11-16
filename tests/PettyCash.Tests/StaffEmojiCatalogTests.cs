using PettyCash.Application.Common;

namespace PettyCash.Tests;

public class StaffEmojiCatalogTests
{
    [Fact]
    public void Accepts_an_emoji_from_the_keyboard()
    {
        Assert.True(StaffEmojiCatalog.TryNormalize(" 😀 ", out var emoji));
        Assert.Equal("😀", emoji);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("hello")]
    [InlineData("<img>")]
    [InlineData("😀😀")]
    public void Rejects_anything_outside_the_list(string? value)
    {
        Assert.False(StaffEmojiCatalog.TryNormalize(value, out _));
    }

    [Fact]
    public void Every_group_has_an_icon_that_is_also_selectable()
    {
        Assert.NotEmpty(StaffEmojiCatalog.Groups);
        foreach (var group in StaffEmojiCatalog.Groups)
        {
            Assert.Contains(group.Icon, group.Emojis);
            Assert.All(group.Emojis, emoji => Assert.True(StaffEmojiCatalog.TryNormalize(emoji, out _)));
        }
    }
}
