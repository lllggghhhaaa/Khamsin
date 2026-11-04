using Xunit;

namespace Khamsin.Blazor.Tests;

/// <summary>Runtime semantics of the clsx-style composer.</summary>
public class CnTests
{
    [Fact]
    public void Of_joins_tokens_in_order()
    {
        Assert.Equal("p-4 flex", Cn.Of("p-4", "flex").Value);
    }

    [Fact]
    public void Of_filters_falsy_inputs()
    {
        Assert.Equal("p-4 flex", Cn.Of(null, "", false, "   ", "p-4", true, "flex").Value);
    }

    [Fact]
    public void Condition_tuples_pick_the_active_branch()
    {
        Assert.Equal("bg-forest-600", Cn.Of((true, "bg-forest-600"), (false, "bg-ember-600")).Value);
        Assert.Equal("bg-ember-600", Cn.Of((false, "bg-forest-600"), (true, "bg-ember-600")).Value);
        Assert.Equal("text-sm", Cn.Of((true, "text-sm", "text-lg")).Value);
        Assert.Equal("text-lg", Cn.Of((false, "text-sm", "text-lg")).Value);
    }

    [Fact]
    public void Later_classes_win_conflicts()
    {
        Assert.Equal("p-8", Cn.Of("p-4", "p-8").Value);
        Assert.Equal("hover:bg-forest-600", Cn.Of("hover:bg-ocean-600", "hover:bg-forest-600").Value);
        Assert.Equal("p-8 flex", Cn.Of("p-4 p-8", "flex").Value);
    }

    [Fact]
    public void Different_groups_coexist()
    {
        Assert.Equal("p-4 flex", Cn.Of("p-4", "flex").Value);
        Assert.Equal("p-4 hover:bg-ocean-600", Cn.Of("p-4", "hover:bg-ocean-600").Value);
    }

    [Fact]
    public void KhamsinClass_values_pass_through()
    {
        KhamsinClass defaults = "p-4 text-sm";
        Assert.Equal("p-4 text-sm", Cn.Of(defaults).Value);
    }

    [Fact]
    public void Merge_applies_inputs_over_defaults()
    {
        var merged = Cn.Merge("p-4 rounded-lg bg-white", "p-8", "rounded-full");
        Assert.Equal("bg-white p-8 rounded-full", merged.Value);
    }

    [Fact]
    public void Nested_collections_flatten()
    {
        var nested = new object?[] { "p-4", new object?[] { (true, "flex"), null }, new object?[] { "text-sm" } };
        Assert.Equal("p-4 flex text-sm", Cn.Of(nested).Value);
    }

    [Fact]
    public void Unknown_author_classes_survive()
    {
        Assert.Equal("my-widget p-4", Cn.Of("my-widget", "p-4").Value);
    }

    [Fact]
    public void Empty_inputs_yield_empty()
    {
        Assert.True(Cn.Of().IsEmpty);
        Assert.True(Cn.Of(null).IsEmpty);
        Assert.True(Cn.Of("").IsEmpty);
    }

    [Fact]
    public void Whitespace_is_trimmed()
    {
        Assert.Equal("p-4 flex", Cn.Of("  p-4  ", "\tflex\t").Value);
    }
}
