namespace Khamsin.Core.Tests;

/// <summary>Byte-level snapshots for the reference stylesheet and its theme layer.</summary>
public class SnapshotTests
{
    [Fact]
    public Task Reference_stylesheet() => Verify(KhamsinEngine.Default.GenerateCss(new[]
    {
        "p-4", "px-6", "py-2", "mt-8", "-ml-2", "mx-auto",
        "flex", "grid", "grid-cols-3", "items-center", "justify-between", "gap-4",
        "w-full", "h-10", "max-w-md", "size-8", "w-1/3",
        "text-sm", "text-ocean-600", "font-semibold", "leading-tight", "truncate",
        "bg-ink-50", "bg-ocean-600/50", "border", "border-ink-200", "rounded-lg", "shadow-md",
        "opacity-75", "ring-2", "ring-ocean-500", "outline", "outline-offset-2",
        "transition", "duration-200", "ease-out", "cursor-pointer", "select-none",
        "hover:bg-ocean-700", "focus-visible:outline", "disabled:opacity-50",
        "dark:bg-ink-900", "md:flex", "lg:grid-cols-4", "group-hover:text-white", "peer-checked:border-forest-500",
        "space-y-2", "!p-1", "w-[320px]", "w-(--panel)",
    }));

    [Fact]
    public Task Variant_matrix() => Verify(KhamsinEngine.Default.GenerateCss(new[]
    {
        "first:mt-0", "last:mb-0", "odd:bg-sand-100", "even:bg-sand-50",
        "sm:p-1", "md:p-2", "lg:p-3", "xl:p-4", "2xl:p-5",
        "hover:bg-ocean-500", "focus:bg-ocean-600", "active:bg-ocean-700", "disabled:cursor-not-allowed",
        "dark:bg-ink-950", "dark:hover:bg-ink-800",
        "group-hover:bg-sand-200", "group-focus:ring-2", "peer-hover:bg-sand-300", "peer-disabled:opacity-50",
    }));
}
