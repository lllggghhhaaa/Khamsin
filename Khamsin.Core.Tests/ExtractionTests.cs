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

        Assert.Equal(["p-4", "p-8"], literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Variant_collects_the_class_of_every_tuple()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.Variant(size, (\"sm\", \"px-2\"), (\"lg\", \"px-6\"));");

        Assert.Equal(["px-2", "px-6"], literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Variant_accepts_single_element_tuples_and_whitespace()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.Variant(size, ( \"sm\" , \"px-2\" ));");

        Assert.Equal(["px-2"], literals.Select(literal => literal.Value));
    }

    [Fact]
    public void Interpolated_strings_are_ignored()
    {
        var literals = CSharpSourceScanner.FindKxLiterals(
            "var classes = Kx.When(flag, $\"p-{size}\", \"p-8\");");

        Assert.Equal(["p-8"], literals.Select(literal => literal.Value));
    }

    [Fact]
    public void KhamsinClass_fields_are_collected_as_defaults()
    {
        var literals = CSharpSourceScanner.FindKhamsinClassLiterals(
            "private static readonly KhamsinClass Defaults = \"rounded-lg p-4\";");

        Assert.Equal(["rounded-lg p-4"], literals.Select(literal => literal.Value));
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

    [Fact]
    public void Razor_typeparam_directives_include_constraints()
    {
        var scan = RazorSourceScanner.Scan(
            """
            @typeparam TValue
            @typeparam TItem where TItem : class, new()
            <div class="p-4">Body</div>
            """);

        Assert.Collection(
            scan.TypeParameters,
            parameter =>
            {
                Assert.Equal("TValue", parameter.Name);
                Assert.Equal(string.Empty, parameter.Constraints);
            },
            parameter =>
            {
                Assert.Equal("TItem", parameter.Name);
                Assert.Equal("class, new()", parameter.Constraints);
            });
    }

    [Fact]
    public void Typeparam_inside_a_comment_is_ignored()
    {
        var scan = RazorSourceScanner.Scan(
            """
            @* @typeparam TIgnored *@
            <div class="p-4">Body</div>
            """);

        Assert.Empty(scan.TypeParameters);
    }

    [Fact]
    public void Interpolated_class_attributes_contribute_literal_fragments()
    {
        var scan = RazorSourceScanner.Scan(
            "<div class=\"@($\"wax-markdown mt-1 text-sm {(Grouped ? \"-mt-1\" : \"\")} sm:text-base\")\">Body</div>");

        var values = scan.AuthorLiterals.Select(literal => literal.Value).ToList();

        Assert.Contains("wax-markdown", values);
        Assert.Contains("mt-1", values);
        Assert.Contains("-mt-1", values);
        Assert.Contains("sm:text-base", values);
    }

    [Fact]
    public void Class_name_parameters_are_scanned()
    {
        var scan = RazorSourceScanner.Scan("<LucideIcon Name=\"loader-circle\" ClassName=\"animate-spin\" />");

        Assert.Contains("animate-spin", scan.AuthorLiterals.Select(literal => literal.Value));
    }

    [Fact]
    public void Bound_class_attributes_resolve_member_expressions()
    {
        const string razor = @"
            @{ var stageFrame = expanded ? ""fixed top-20 z-[70]"" : ""bottom-24 sm:w-96""; }
            <div id=""stage"" class=""@stageFrame""></div>";

        var scan = RazorSourceScanner.Scan(razor);
        var values = scan.AuthorLiterals.Select(literal => literal.Value).ToList();

        Assert.Contains("z-[70]", values);
        Assert.Contains("sm:w-96", values);
        Assert.Empty(scan.References);
    }

    [Fact]
    public void Unresolved_class_references_are_reported()
    {
        var scan = RazorSourceScanner.Scan("<div class=\"@MissingClass\"></div>");

        Assert.Contains(scan.References, reference => reference.Name == "MissingClass");
    }

    [Fact]
    public void Composed_csharp_strings_are_collected()
    {
        const string code = @"
            internal string MobileNavClass => ""min-h-0 shrink-0 "" + (open
                ? ""fixed inset-y-0 left-0 z-[45] flex""
                : ""hidden md:flex"");";

        var values = CSharpSourceScanner
            .FindClassCompositionLiterals(code, token => token is "flex" or "hidden" or "z-[45]" or "md:flex" or "min-h-0" or "shrink-0" or "fixed" or "inset-y-0" or "left-0")
            .Select(literal => literal.Value)
            .ToList();

        Assert.Contains(values, value => value.Contains("z-[45]"));
        Assert.Contains(values, value => value.Contains("md:flex"));
    }

    [Fact]
    public void Markup_strings_contribute_embedded_class_attributes()
    {
        const string code = @"renderer.Write(""<img class=\""inline-block h-5 w-5 align-text-bottom\"" />"");";

        var values = CSharpSourceScanner
            .FindClassCompositionLiterals(code, token => token is "inline-block" or "h-5" or "w-5" or "align-text-bottom")
            .Select(literal => literal.Value)
            .ToList();

        Assert.Contains(values, value => value.Contains("align-text-bottom"));
    }

    [Fact]
    public void Prose_strings_are_not_class_lists()
    {
        var literals = CSharpSourceScanner.FindClassCompositionLiterals(
            "var message = \"Use 2-32 lowercase letters, numbers or underscores.\";",
            token => token is "lowercase" or "numbers" or "underscores");

        Assert.Empty(literals);
    }
}
