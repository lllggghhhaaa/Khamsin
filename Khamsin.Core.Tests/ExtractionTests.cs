using Khamsin.Extraction;

namespace Khamsin.Core.Tests;

/// <summary>Covers the lexical scanners the MSBuild task relies on where no compilation exists.</summary>
public class ExtractionTests
{
    [Fact]
    public void When_collects_both_branches()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.When(flag, \"p-4\", \"p-8\");");

        Assert.Equal(new[] { "p-4", "p-8" }, literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Variant_collects_the_class_of_every_tuple()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.Variant(size, (\"sm\", \"px-2\"), (\"lg\", \"px-6\"));");

        Assert.Equal(new[] { "px-2", "px-6" }, literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Variant_accepts_single_element_tuples_and_whitespace()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.Variant(size, ( \"sm\" , \"px-2\" ));");

        Assert.Equal(new[] { "px-2" }, literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Interpolated_strings_are_ignored()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.When(flag, $\"p-{size}\", \"p-8\");");

        Assert.Equal(new[] { "p-8" }, literals.Select(literal => literal.Value));
    }

    [Fact]
    public void KhamsinClass_fields_are_collected_as_defaults()
    {
        var literals = CSharpSourceScanner.FindKhamsinClassLiterals(
            "private static readonly KhamsinClass Defaults = \"rounded-lg p-4\";");

        Assert.Equal(new[] { "rounded-lg p-4" }, literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Razor_code_blocks_contribute_kx_and_default_literals()
    {
        var scan = RazorSourceScanner.Scan(
            """
            <section class="@(Defaults + Khamsin)">Body</section>

            @code {
                private static readonly KhamsinClass Defaults = "rounded-lg p-4";
                private static readonly KhamsinClass Status = Kx.Variant("warn", ("ok", "text-forest-600"), ("warn", "text-amber-600"));
            }
            """);

        var values = scan.Literals.Select(literal => literal.Value).ToList();

        Assert.Contains("p-4", values);
        Assert.Contains("text-forest-600", values);
        Assert.Contains("text-amber-600", values);
    }

    [Fact]
    public void Render_fragments_in_code_blocks_contribute_author_classes()
    {
        var scan = RazorSourceScanner.Scan(
            """
            <div class="gap-2">Body</div>

            @code {
                private RenderFragment Preview = @<div class="gap-8">
                    <span class="gap-10">Nested</span>
                </div>;

                private const string Escaped = "<div class=\"gap-12\">escaped</div>";
                private const string Verbatim = @"<div class=""gap-14"">verbatim</div>";
            }
            """);

        var values = scan.AuthorLiterals.Select(literal => literal.Value).ToList();

        Assert.Contains("gap-2", values);
        Assert.Contains("gap-8", values);
        Assert.Contains("gap-10", values);
        Assert.DoesNotContain("gap-12", values);
        Assert.DoesNotContain("gap-14", values);
    }
}
