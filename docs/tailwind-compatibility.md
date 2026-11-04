# Tailwind compatibility

Khamsin implements the Tailwind utility and variant API - v3.4 plus the v4 core (masks, 3D
transforms, container queries, negation, v4 shadows, `data-open:`-style shorthands). Exact class
lists live in [utilities.md](utilities.md); configuration in [config.md](config.md). This page
lists only what Tailwind has that Khamsin does not.

## Not supported

**By design** - the closed-set model replaces them:

- Tailwind/JS config file, plugins, content scanning, `safelist` (use `[KhamsinManifest]`),
  runtime class generation. The set of possible classes is closed at build time; dynamic choices
  go through `Kx.When`/`Kx.Variant`, whose literal branches are extracted.
- `@apply` and CSS-first `@theme` (the JSON config in khamsin.json covers theming).
- v4 scale renames (`shadow-2xs/xs`, `rounded-4xs`, remapped text sizes) - the v3 scale names stand.

**Behavioral differences** worth knowing:

- Unknown classes in typed positions (`Khamsin=`, `Kx.*`, `KhamsinClass` literals) are build
  errors (KHM002); in plain `class="…"` attributes they are silently ignored.
- `dark:` uses the class strategy by default; `"dark": "media"` in khamsin.json switches to
  `prefers-color-scheme`. There is no per-utility gating of `hover:` behind `(hover: hover)`.
- Arbitrary values: `text-[…]` always binds to font size, `border-[…]` to color, `font-[…]` to
  family. Use arbitrary properties (`[color:…]`) or the dedicated utilities for the rest.
  `bg-[url(…)]` and gradients detect as background-image.
- Gradients emit `color-mix`-based stops; drop-, inset- and text-shadows recolor through
  `--khamsin-*-color` variables rather than Tailwind's shadow-color machinery.
- Variants: `details-content:` is supported; `@min-[…]:` (arbitrary container size) and
  `group-not-*`/`peer-not-*` composition remain **not supported**.
- Trailing `!` (the v3 important form) is supported alongside the v4 leading form.
- `space-*`/`divide-*-reverse` (logical properties already handle row-reverse layouts).

## Coming from a Tailwind build

1. **Delete the toolchain.** Remove `tailwind.config.js`, the `@tailwind`/PostCSS steps and npm.
   Reference the Khamsin package and serve `khamsin.css` (static web asset, or `app.MapKhamsinCss()`).
2. **Replace dynamic class strings.** `class="p-@size"` fails with KHM001. Use `Kx.When`/`Kx.Variant`
   so every alternative is a literal; keep runtime values in CSS variables (`w-(--w)` + `style="--w:…"`).
3. **Pick a dark strategy.** Default `dark:` emits `.dark <selector>`; `"dark": "media"` follows
   `prefers-color-scheme` (see [config.md](config.md)).
4. **No preflight.** Khamsin emits no global reset; add your own before the layers. Border
   utilities set `border-style` themselves for this reason.
5. **Unknown utilities are KHM002 errors** in `Khamsin=`/`Kx.*` positions (see
   [analyzer-rules.md](analyzer-rules.md)); in plain `class` attributes they are ignored.
6. **`@apply` and plugins do not exist.** Write the rule by hand, or add a registry entry when the
   utility belongs in the closed set.

When moving from a hand-rolled merge helper: the later class wins within its conflict group even if
the earlier one has higher CSS specificity, so review deliberately stacked utilities of the same
family. Component defaults should live in a `KhamsinClass` field so consumers can override them.

## Runtime API (no Tailwind equivalent)

| API | Purpose |
| --- | --- |
| `KhamsinClass` | Typed class value; implicit from `string`; `+`, `Merge`, `Join` with conflict resolution. |
| `Kx.When` / `Kx.Variant` | Literal-branch conditional / literal-option switch. |
| `KhamsinComponentBase` | `Khamsin` parameter for consumer overrides. |
| `[KhamsinManifest]` | Manual classes and render-tree edges for tree shaking. |
| `MapKhamsinCss()` | Endpoint fallback with ETag caching. |
