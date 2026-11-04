using Microsoft.AspNetCore.Components;

namespace Khamsin;

/// <summary>
/// Optional base class that adds the <see cref="Khamsin"/> parameter. Components can either derive
/// from it or declare the parameter themselves; the analyzer treats both identically.
/// </summary>
public abstract class KhamsinComponentBase : ComponentBase
{
    /// <summary>Consumer-supplied utility classes, merged over the component's defaults.</summary>
    [Parameter]
    public KhamsinClass Khamsin { get; set; }
}
