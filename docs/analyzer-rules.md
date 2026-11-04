# Analyzer rules

The analyzer ships in `Khamsin.Generators` and runs in every project that references the package.

| Id | Severity | Meaning |
| --- | --- | --- |
| KHM001 | Error | Khamsin class input is not a string literal. |
| KHM002 | Error | A class is not part of the utility registry. |
| KHM003 | Warning | Two classes in one literal target the same conflict group and variant set. |
| KHM004 | Info | A component tag in markup could not be resolved, so its classes cannot be discovered. |
| KHM005 | Error | `khamsin.json` could not be parsed or contains an invalid entry. |

**KHM001** - the class set is closed at build time, so inputs must be literals. Use `Kx.When` /
`Kx.Variant`, or the clsx-style composer `Cn.Of(...)`, whose strings, ternary branches, tuples and
nested collections are all extracted; only dynamic strings are rejected:

```razor
<div Khamsin="@Kx.When(compact, "p-2", "p-4")">…</div>
<div Khamsin="w-(--panel)" style="--panel: 18rem;">…</div>
```

**KHM002** - the token produces no CSS. Only typed positions are checked (`Khamsin=` attributes,
`Kx.*` calls, `KhamsinClass` fields); plain `class` attributes are an open namespace and their
unknown names are ignored. The code fix removes the unknown token without guessing a replacement.

**KHM003** - two classes in one literal resolve to the same conflict group and variant set; the
merge keeps the later one. Defaults and consumer overrides live in different literals and are never
reported - that is the override mechanism.

**KHM004** - a markup tag could not be mapped to a component type (usually a missing `@using`), so
its classes and nested components are not discovered and its styles may be missing.

**KHM005** - `khamsin.json` failed to parse (the message carries line and column) or contains an
invalid entry. The build falls back to the default theme until the file is fixed. See
[config.md](config.md).

The MSBuild task repeats KHM002 as a build warning for any manifest class that is not in the
registry, and skips it; the build still succeeds.
