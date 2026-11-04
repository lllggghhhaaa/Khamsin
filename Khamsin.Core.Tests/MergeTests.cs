namespace Khamsin.Core.Tests;

public class MergeTests
{
    [Fact]
    public void Later_class_wins_in_the_same_group()
    {
        Assert.Equal("py-2 px-6", KhamsinMerger.Merge("px-4 py-2", "px-6"));
    }

    [Fact]
    public void Parent_group_removes_descendants()
    {
        Assert.Equal("p-4", KhamsinMerger.Merge("px-2 py-2 p-4", ""));
        Assert.Equal("p-4", KhamsinMerger.Merge("", "px-2 p-4"));
    }

    [Fact]
    public void Descendant_group_does_not_remove_parent()
    {
        Assert.Equal("p-4 px-2", KhamsinMerger.Merge("p-4", "px-2"));
    }

    [Fact]
    public void Different_variants_do_not_conflict()
    {
        Assert.Equal("bg-ocean-700 hover:bg-ocean-600", KhamsinMerger.Merge("bg-ocean-700", "hover:bg-ocean-600"));
        Assert.Equal("bg-ocean-700 md:bg-ocean-600", KhamsinMerger.Merge("bg-ocean-700", "md:bg-ocean-600"));
    }

    [Fact]
    public void Important_is_a_separate_variant_set()
    {
        Assert.Equal("!p-4 p-2", KhamsinMerger.Merge("!p-4", "p-2"));
        Assert.Equal("!p-2", KhamsinMerger.Merge("!p-4", "!p-2"));
    }

    [Fact]
    public void Unknown_classes_are_preserved_in_order()
    {
        Assert.Equal("print-only px-6", KhamsinMerger.Merge("px-4 print-only", "px-6"));
    }

    [Fact]
    public void Duplicate_classes_collapse()
    {
        Assert.Equal("px-4", KhamsinMerger.Merge("px-4", "px-4"));
    }

    [Fact]
    public void Empty_inputs_are_handled()
    {
        Assert.Equal(string.Empty, KhamsinMerger.Merge(null, null));
        Assert.Equal("p-4", KhamsinMerger.Merge("p-4", null));
        Assert.Equal("p-4", KhamsinMerger.Merge(null, "p-4"));
    }

    [Fact]
    public void Radius_and_shadow_groups_are_independent()
    {
        Assert.Equal("shadow-md rounded-xl", KhamsinMerger.Merge("rounded-lg shadow-md", "rounded-xl"));
    }

    [Fact]
    public void Size_removes_width_and_height()
    {
        Assert.Equal("size-8", KhamsinMerger.Merge("w-4 h-4", "size-8"));
    }

    [Fact]
    public void Cache_returns_consistent_results()
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal("py-2 px-6", KhamsinMerger.Merge("px-4 py-2", "px-6"));
        }
    }
}
