# Configuration

An optional `khamsin.json` next to the project file (rename or relocate via the `KhamsinConfig`
MSBuild property; set it empty to disable). The analyzer, the generator and the MSBuild CSS task
all read it, and it is an incremental input: editing it regenerates the stylesheet once.

## Schema

```json
{
  "dark": ".theme-dark",
  "spacing": "0.25rem",
  "colors": {
    "primary": "var(--primary)",
    "muted": { "DEFAULT": "var(--muted)", "foreground": "var(--muted-foreground)" }
  },
  "fonts":      { "display": "Georgia, serif" },
  "radius":     { "card": "1.25rem" },
  "shadows":    { "glow": "0 0 12px var(--primary)" },
  "textSizes":  { "display": ["2rem", "2.4rem"], "tiny": "0.6rem" },
  "eases":      { "snap": "cubic-bezier(0.8,0,0.2,1)" },
  "breakpoints": { "3xl": "112rem" },
  "containers":  { "content": "48rem" },
  "animations": {
    "pop": { "value": "pop 0.2s var(--ease-out)", "keyframes": { "from": "opacity:0", "to": "opacity:1" } }
  },
  "css": ["Theme/tokens.css", "app.css"]
}
```

Entries **merge over the built-ins by name**: adding `primary` keeps the palette, redefining
`ocean` replaces that ramp. Color entries are a string or a shade object (`DEFAULT` names the bare
token). Values may be `var(…)` references so runtime theming and dark flips work; `/opacity`
composes via `color-mix`. `breakpoints` entries become `x:` and `max-x:` variants; `animations`
feed `animate-{name}` and their keyframes are tree-shaken. The `dark` key is a selector prefix
(default `.dark`) or `"media"` for `prefers-color-scheme`.

Names are lowercase letters/digits/dashes; values must not contain `;`, `{`, `}`, `@`. Invalid
entries are **KHM005** build errors.

## CSS bundling

The optional `css` list points at stylesheets (relative to the project) that ship **inside the
generated `khamsin.css`**, in a `base` cascade layer between the theme and the utilities - so
theme variables and hand-written rules load through the one stylesheet the app already links
(`MapKhamsinCss()` or the static asset), with no separate `tokens.css`/`app.css` files to serve.
Files resolve by name against the project's CSS files; the list is an incremental input too.

## Runtime merging

The generator emits a `Khamsin.ThemeRegistration.g.cs` with a `[ModuleInitializer]` that calls
`KhamsinMerger.Configure(config)` - only in the entry assembly, so libraries never fight the app.
Without it, runtime conflict resolution would not know custom tokens (`bg-primary` would not
replace a default `bg-white`). First call wins.

## Library configurations

Every project runs the generator, including class libraries. A library with its own `khamsin.json`
embeds the document as a `[KhamsinConfig(Json = "…")]` assembly attribute. When an app references
that library, the app's generator and CSS task read the attribute and merge it under the app's own
configuration, so library tokens validate and emit without being copied into the app. The library's
`css` files travel the same way and land in the app's `base` layer.

The merge order is deterministic:

1. Khamsin's built-in defaults.
2. Referenced assemblies' configurations, in assembly-name order.
3. The current project's `khamsin.json`.

Later entries win by name, so an app can override a single library token without restating the
rest. This works the same for `ProjectReference` and `PackageReference` consumers.

## Example: semantic tokens

```json
{
  "dark": ".theme-dark",
  "colors": {
    "background": "var(--background)",
    "foreground": "var(--foreground)",
    "primary": { "DEFAULT": "var(--primary)", "foreground": "var(--primary-foreground)" }
  }
}
```

Define the variables in your own CSS (`:root` and `.theme-dark`) and `bg-primary`,
`text-muted-foreground`, `ring-destructive/40` build, validate, merge and re-theme instantly.
