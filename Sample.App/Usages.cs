using Khamsin;

namespace Sample.App;

/// <summary>Severity levels driving <see cref="CSharpExamples.StatusClasses"/>.</summary>
public enum Severity
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Khamsin extraction is not limited to Razor files. Plain C# contributes classes the same way:
/// string literals in <see cref="Kx.When"/>/<see cref="Kx.Variant"/> calls and initializers of
/// <see cref="KhamsinClass"/>-typed members are all compiled into the stylesheet.
/// </summary>
[KhamsinManifest(Classes = "ring-8 ring-violet-300 bg-violet-50")]
public static class CSharpExamples
{
    /// <summary>A member initializer: the simplest extraction form in C# sources.</summary>
    public static readonly KhamsinClass AccentBox = "bg-violet-600 px-4 py-2 rounded-md text-white shadow-md";

    /// <summary>Both branches of the condition are extracted; the choice happens at runtime.</summary>
    public static KhamsinClass Toggle(bool on) => Kx.When(on, "border-forest-500 bg-forest-50 text-forest-900", "border-ink-300 bg-ink-50 text-ink-500");

    /// <summary>A non-string selector value: options stay literal, the key can be anything.</summary>
    public static KhamsinClass StatusClasses(Severity severity) => Kx.Variant(
        severity,
        (Severity.Info, "bg-ocean-100 text-ocean-800"),
        (Severity.Success, "bg-forest-100 text-forest-800"),
        (Severity.Warning, "bg-amber-100 text-amber-800"),
        (Severity.Error, "bg-ember-100 text-ember-800"));

    /// <summary>Composing values: the merge operator resolves padding conflicts, later winning.</summary>
    public static string MergeDemo() => KhamsinClass.Merge("p-4 rounded-lg", "p-8 rounded-full").Value;

    /// <summary>
    /// Variant names containing an at-sign must live in C# strings: Razor reads a bare @ in markup
    /// as the start of an expression, so these classes cannot be written in a class attribute.
    /// </summary>
    public static class ContainerQueries
    {
        public static readonly KhamsinClass Container = "@container";
        public static readonly KhamsinClass Responsive = "grid grid-cols-1 gap-2 @sm:grid-cols-2 @2xl:grid-cols-4";
    }

    public static class ArbitraryVariants
    {
        public static readonly KhamsinClass LargeScreensOnly = "[@media(min-width:64rem)]:inline";
        public static readonly KhamsinClass SmallScreensOnly = "[@media(min-width:64rem)]:hidden";
        public static readonly KhamsinClass HoverDevicesOnly = "[@media(hover:hover)]:inline";
        public static readonly KhamsinClass NonHoverDevicesOnly = "[@media(hover:hover)]:hidden";
        public static readonly KhamsinClass WideBandOnly = "[@media(min-width:80rem)and(max-width:90rem)]:block";
        public static readonly KhamsinClass ContainerThreeCols = "[@container(min-width:28rem)]:grid-cols-3";
    }
}
