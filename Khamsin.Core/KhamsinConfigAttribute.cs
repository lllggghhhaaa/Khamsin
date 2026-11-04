namespace Khamsin;

/// <summary>
/// Build-time configuration attached to an assembly by the source generator. Consumers merge the
/// configuration documents of referenced assemblies under their own, so a component library can
/// ship theme tokens without asking the application to copy them.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class KhamsinConfigAttribute : Attribute
{
    /// <summary>The raw <c>khamsin.json</c> document embedded for this assembly.</summary>
    public string Json { get; set; } = string.Empty;
}
