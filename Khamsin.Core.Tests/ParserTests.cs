namespace Khamsin.Core.Tests;

public class ParserTests
{
    private static ParsedClass Parse(string token)
    {
        Assert.True(KhamsinEngine.Default.TryParse(token, out var parsed, out var error), error?.Message);
        Assert.NotNull(parsed);
        return parsed;
    }

    [Fact]
    public void Parses_variants_important_value_and_opacity()
    {
        var parsed = Parse("dark:md:hover:!bg-ocean-600/50");

        Assert.Equal(new[] { "dark", "md", "hover" }, parsed.Variants);
        Assert.True(parsed.Important);
        Assert.Equal("bg", parsed.Utility);
        Assert.Equal("ocean-600", parsed.Value);
        Assert.Equal("50", parsed.Modifier);
        Assert.Equal(80, parsed.VariantWeight);
        Assert.False(parsed.Negative);
    }

    [Fact]
    public void Parses_negative_spacing()
    {
        var parsed = Parse("-mt-4");

        Assert.True(parsed.Negative);
        Assert.Equal("mt", parsed.Utility);
        Assert.Equal("4", parsed.Value);
    }

    [Fact]
    public void Parses_fractions_for_sizing()
    {
        var parsed = Parse("w-1/3");

        Assert.Equal("w", parsed.Utility);
        Assert.Equal("1/3", parsed.Value);
        Assert.Null(parsed.Modifier);
    }

    [Fact]
    public void Parses_arbitrary_values()
    {
        var parsed = Parse("w-[320px]");

        Assert.True(parsed.IsArbitrary);
        Assert.Equal("w", parsed.Utility);
        Assert.Equal("[320px]", parsed.Value);
    }

    [Fact]
    public void Parses_css_variable_shorthand()
    {
        var parsed = Parse("w-(--sidebar-width)");

        Assert.True(parsed.IsCssVariable);
        Assert.Equal("w", parsed.Utility);
        Assert.Equal("(--sidebar-width)", parsed.Value);
    }

    [Fact]
    public void Disambiguates_text_prefixes()
    {
        Assert.Equal("text-size", Parse("text-sm").Entry.Group);
        Assert.Equal("text-align", Parse("text-center").Entry.Group);
        Assert.Equal("text-color", Parse("text-ocean-600").Entry.Group);
    }

    [Fact]
    public void Parses_important_static_utility()
    {
        var parsed = Parse("!flex");

        Assert.True(parsed.Important);
        Assert.Equal("flex", parsed.Utility);
        Assert.Null(parsed.Value);
    }

    [Fact]
    public void Parses_space_utility_child_selector()
    {
        var parsed = Parse("space-y-4");

        Assert.Equal(">*+*", parsed.Entry.ChildSelector);
    }

    [Fact]
    public void Parses_arbitrary_aspect_ratio()
    {
        var parsed = Parse("aspect-[16/9]");

        Assert.True(parsed.IsArbitrary);
        Assert.Equal("aspect-ratio:{v}", parsed.Entry.ArbitraryTemplate ?? parsed.Entry.Template);
    }

    [Theory]
    [InlineData("unknown:hover:bg-ocean-600")]
    [InlineData("bg-nope-500")]
    [InlineData("p-")]
    [InlineData("w-[320px")]
    [InlineData("")]
    [InlineData("hover::p-4")]
    [InlineData("-p-4")]
    public void Rejects_invalid_tokens(string token)
    {
        Assert.False(KhamsinEngine.Default.TryParse(token, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Class_names_are_case_sensitive()
    {
        Assert.False(KhamsinEngine.Default.TryParse("Hover:bg-ocean-600", out _, out _));
        Assert.False(KhamsinEngine.Default.TryParse("BG-ocean-600", out _, out _));
    }
}
