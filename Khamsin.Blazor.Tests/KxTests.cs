using Xunit;

namespace Khamsin.Blazor.Tests;

public sealed class KxTests
{
    [Theory]
    [InlineData(true, "p-2")]
    [InlineData(false, "p-4")]
    public void When_selects_one_literal(bool condition, string expected)
    {
        var result = Kx.When(condition, "p-2", "p-4");

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void When_defaults_to_empty_false_branch()
    {
        Assert.True(Kx.When(false, "p-2").IsEmpty);
    }

    [Theory]
    [InlineData("sm", "px-2")]
    [InlineData("lg", "px-6")]
    [InlineData("xl", "")]
    public void Variant_matches_by_value(string value, string expected)
    {
        var result = Kx.Variant(value, ("sm", "px-2"), ("lg", "px-6"));

        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Variant_supports_enums_and_null()
    {
        Assert.Equal("py-1", Kx.Variant(Size.Small, (Size.Small, "py-1"), (Size.Large, "py-3")).Value);
        Assert.Equal("p-0", Kx.Variant<string?>(null, (null, "p-0"), ("x", "p-1")).Value);
    }

    private enum Size
    {
        Small,
        Large,
    }
}
