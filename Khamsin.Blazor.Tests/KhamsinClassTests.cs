using Xunit;

namespace Khamsin.Blazor.Tests;

public sealed class KhamsinClassTests
{
    [Fact]
    public void Converts_from_string()
    {
        KhamsinClass classes = "p-4 text-sm";

        Assert.Equal("p-4 text-sm", classes.Value);
        Assert.False(classes.IsEmpty);
    }

    [Theory]
    [InlineData("px-4", "px-6", "px-6")]
    [InlineData("p-4 bg-white", "p-6", "bg-white p-6")]
    [InlineData("px-4 py-2", "px-6", "py-2 px-6")]
    [InlineData("p-4", "px-6", "p-4 px-6")]
    [InlineData("px-6", "p-2", "p-2")]
    [InlineData("p-4 hover:p-6", "p-8", "hover:p-6 p-8")]
    [InlineData("hover:p-4", "hover:p-6", "hover:p-6")]
    [InlineData("!px-4", "px-6", "!px-4 px-6")]
    [InlineData("px-4", "!px-6", "px-4 !px-6")]
    [InlineData("custom-class", "px-3", "custom-class px-3")]
    [InlineData("", "px-3", "px-3")]
    [InlineData("px-3", "", "px-3")]
    public void Merges_with_later_winning(string defaults, string overrides, string expected)
    {
        KhamsinClass result = new KhamsinClass(defaults) + overrides;

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Join_merges_every_argument_in_order()
    {
        var joined = KhamsinClass.Join("p-4", null, "px-6", "px-2");

        Assert.Equal("p-4 px-2", joined.Value);
    }

    [Fact]
    public void Equality_is_ordinal()
    {
        KhamsinClass left = "px-4";
        KhamsinClass right = "px-4";

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Empty_value_is_safe()
    {
        KhamsinClass classes = default;

        Assert.True(classes.IsEmpty);
        Assert.Equal(string.Empty, classes.ToString());
        Assert.Equal(KhamsinClass.Empty, classes);
    }
}
