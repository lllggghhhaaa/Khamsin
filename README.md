# Khamsin

Khamsin is a build-time utility-CSS engine for Blazor. Components expose a dedicated `Khamsin`
parameter; the engine generates a minimal static stylesheet from the classes your app and its used
components actually contain, and merges library defaults with consumer overrides at runtime using
the same registry.

It is written from scratch in C#. It does not run Node, npm or another CSS toolchain, and it uses the
widely documented Tailwind-compatible class syntax without being affiliated with Tailwind CSS.

## Compatibility

The Tailwind utility and variant API — v3.4 plus the v4 core (masks, 3D transforms, container
queries, negation, v4 shadows, attribute shorthands) — with an optional `khamsin.json` theme
config. The JS toolchain, config file, plugins and `@apply` are intentionally replaced by the
closed build-time model. See [docs/utilities.md](docs/utilities.md),
[docs/config.md](docs/config.md) and [docs/tailwind-compatibility.md](docs/tailwind-compatibility.md).

```razor
<PrimaryButton Khamsin="@("px-6")">Save</PrimaryButton>
```

```razor
@inherits KhamsinComponentBase

<button class="@(Defaults + Khamsin)">@ChildContent</button>

@code {
    private static readonly KhamsinClass Defaults = "bg-ocean-600 px-4 py-2 rounded-md text-white";
}
```

The consumer's `px-6` wins over the default `px-4` at runtime; the stylesheet contains both because
both are statically reachable.

## Install

```xml
<PackageReference Include="Khamsin" Version="0.1.0" />
```

The package carries the runtime (`lib/net8.0`), the analyzer and manifest generator
(`analyzers/dotnet/cs`), the MSBuild task (`tasks/net8.0`) and the
`buildTransitive` props/targets. Reference it from every project whose components use Khamsin classes;
libraries get manifests, the executable or web project gets the stylesheet.

Serve the generated file like any other static asset:

```html
<link rel="stylesheet" href="khamsin.css" />
```

For Blazor Server you can serve it from an endpoint instead, which uses an embedded copy with a
content hash and a one-year immutable cache policy:

```csharp
app.MapKhamsinCss();
```

## How the build works

1. The source generator reads `.razor` files as `AdditionalFiles` and emits a
   `[KhamsinManifest(Classes = "…", Uses = new[] { typeof(A) })]` part for every component, plus one
   assembly-level manifest for `Kx` literals in helper classes.
2. After `CoreCompile`, the MSBuild task opens the consumer assembly and finds the components it
   renders through `RenderTreeBuilder.OpenComponent<T>` metadata.
3. It follows each component's `Uses` transitively through referenced assemblies, adds the
   consumer's own sources, and writes `obj/<configuration>/<tfm>/khamsin.css` containing only those
   classes. Components from a library that the app never renders contribute nothing.
4. The file is registered as a static web asset (and copied to the output for other project types).

Output is minified by default. Setting `KhamsinMinify` to `false` writes indented CSS with one
declaration per line; the class set and ordering are identical in both forms.

The task is incremental: a no-op build skips it, and identical CSS is not rewritten.

### MSBuild properties

| Property | Default | Purpose |
| --- | --- | --- |
| `KhamsinEmitCss` | `true` for web/executable projects, `false` otherwise | Emit the stylesheet for this project. |
| `KhamsinMinify` | `true` | Write minified CSS; set `false` for indented output while debugging. |
| `KhamsinCssFileName` | `khamsin.css` | File name registered as a static asset. |
| `KhamsinCssOutputPath` | `$(IntermediateOutputPath)$(KhamsinCssFileName)` | Override the generated file location. |
| `KhamsinRegisterStaticWebAsset` | `true` | Register the file with the static web asset pipeline. |
| `KhamsinAddRazorAdditionalFiles` | `true` | Feed `**/*.razor` to the generator. |
| `KhamsinConfig` | `khamsin.json` | Theme/token configuration file; see [docs/config.md](docs/config.md). |
| `KhamsinTaskAssembly` | set by the package | Task assembly path; used by repository samples. |

## The closed class set

Every class that can render must be a literal in the source:

```razor
<div Khamsin="p-4 md:p-6">…</div>
```

Dynamic choices go through the literal-based helpers, which are equally extractable:

```razor
<div Khamsin="@Kx.When(compact, "p-2", "p-4")">…</div>
<div Khamsin="@Kx.Variant(size, ("sm", "px-2"), ("lg", "px-6"))">…</div>
```

Runtime values use CSS custom properties, not string building:

```razor
<div Khamsin="w-(--panel)" style="--panel: 18rem;">…</div>
```

For clsx-style composition there is `Cn.Of(...)`, which accepts strings, booleans, condition
tuples, nested collections and `KhamsinClass` values, filters to the truthy parts and resolves
conflicts last-wins — every string in it must still be a literal so the build can extract it:

```razor
<div class="@Cn.Of("p-4", compact ? "p-2" : "p-8", (primary, "bg-ocean-600"))">…</div>
```

Interpolation, concatenation and variables flowing into `Khamsin` or `Kx.*` fail the build with
KHM001. See [docs/utilities.md](docs/utilities.md) for the supported classes and variants and
[docs/analyzer-rules.md](docs/analyzer-rules.md) for the diagnostics.

## Repository layout

```
Khamsin.Core/            netstandard2.0 engine: parser, registry, theme, emitter, merge
Khamsin.Generators/      netstandard2.0 incremental generator, analyzer and code fix
Khamsin.Build/           net8.0 MSBuild task
Khamsin.Blazor/          net8.0 runtime: KhamsinClass, Kx, merge cache, MapKhamsinCss
Khamsin/                 packaging-only metapackage
Khamsin.Core.Tests/      engine tests
Khamsin.Generators.Tests/ generator, analyzer and manifest tests
Khamsin.Blazor.Tests/    runtime tests
Khamsin.Build.Tests/     MSBuild integration tests
Sample.Lib/              sample Razor class library
Sample.App/              sample Blazor Server app
docs/                   utilities, config, Tailwind compatibility and migration, analyzer rules
```

Every project lives in the repository root and is listed in `Khamsin.slnx`.

## Building and testing

```bash
dotnet build Khamsin.slnx
dotnet test Khamsin.slnx
dotnet pack Khamsin/Khamsin.csproj -c Release
```

Two environment notes:

- The test projects target `net8.0` and reference `Microsoft.AspNetCore.App`. On a machine without
  the ASP.NET Core 8.0 runtime, run the tests with `DOTNET_ROLL_FORWARD=LatestMajor`.

Coming from a Tailwind build? [docs/tailwind-compatibility.md](docs/tailwind-compatibility.md)
lists the differences and the migration steps.

## License

Apache License 2.0 — see [LICENSE](LICENSE).
