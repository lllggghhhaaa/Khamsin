using Khamsin.Extraction;

namespace Khamsin.Core.Tests;

/// <summary>Lexical Cn extraction used by the MSBuild task and the razor scan.</summary>
public class CnScannerTests
{
    [Fact]
    public void Cn_calls_collect_all_argument_literals()
    {
        var literals = CSharpSourceScanner.FindKxLiterals("""Cn.Of("p-4", isActive ? "bg-forest-600" : "bg-ember-600")""");
        var values = literals.Select(l => l.Value).ToList();
        Assert.Contains("p-4", values);
        Assert.Contains("bg-forest-600", values);
        Assert.Contains("bg-ember-600", values);
        Assert.All(literals, l => Assert.Equal(KxCallKind.Cn, l.Kind));
    }

    [Fact]
    public void Cn_merge_collects_defaults_and_overrides()
    {
        var literals = CSharpSourceScanner.FindKxLiterals("""Cn.Merge(defaults, "p-8", other ? "rounded-full" : null)""");
        var values = literals.Select(l => l.Value).ToList();
        Assert.Contains("p-8", values);
        Assert.Contains("rounded-full", values);
    }

    [Fact]
    public void Nested_collections_and_tuples_are_scanned()
    {
        var literals = CSharpSourceScanner.FindKxLiterals("""Cn.Of(new object?[] { "p-4", (true, "flex") })""");
        var values = literals.Select(l => l.Value).ToList();
        Assert.Contains("p-4", values);
        Assert.Contains("flex", values);
    }

    [Fact]
    public void Razor_markup_with_cn_is_scanned()
    {
        var scan = RazorSourceScanner.Scan("""<div class="static @(Cn.Of("p-4", cond ? "flex" : "grid"))">x</div>""");
        Assert.Contains("static", scan.AuthorLiterals.Select(l => l.Value).ToList());
        var values = scan.Literals.Select(l => l.Value).ToList();
        Assert.Contains("p-4", values);
        Assert.Contains("flex", values);
        Assert.Contains("grid", values);
    }

    [Fact]
    public void Khamsin_attribute_with_cn_call_is_not_flagged_non_literal()
    {
        var scan = RazorSourceScanner.Scan("""<div Khamsin="@(Cn.Of(SomeClasses()))">x</div>""");
        Assert.Empty(scan.NonLiteralUsages);
    }

    [Fact]
    public void Identifiers_starting_with_cn_are_ignored()
    {
        Assert.Empty(CSharpSourceScanner.FindKxLiterals("""CnMerge.Of("p-4")"""));
        Assert.Empty(CSharpSourceScanner.FindKxLiterals("""Scan.Of("p-4")"""));
    }

    [Fact]
    public void Interpolated_strings_are_still_rejected()
    {
        Assert.Empty(CSharpSourceScanner.FindKxLiterals("""Cn.Of($"p-{size}")"""));
    }
}
