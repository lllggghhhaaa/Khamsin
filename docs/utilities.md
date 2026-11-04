# Utilities

The closed class set. The registry in `Khamsin.Core/BuiltInUtilities.cs` is the source of truth;
unknown classes in typed positions are KHM002 errors, unknown classes in plain `class="..."`
attributes are ignored (they are assumed to be your own CSS). Token entries from `khamsin.json`
extend the set - see [config.md](config.md). For the few Tailwind features Khamsin does not have,
see [tailwind-compatibility.md](tailwind-compatibility.md).

## Class syntax

| Form | Example |
| --- | --- |
| Variants | `md:flex`, `md:hover:bg-ocean-700` |
| Important (leading) | `!p-2` |
| Negative | `-mt-4`, `-rotate-45` |
| Spacing step | `p-4` (times `--spacing`, 0.25rem) |
| Fraction | `w-1/2` |
| Opacity modifier | `bg-ocean-600/50` (color-mix, colors only) |
| Token | `rounded-lg`, `shadow-md`, `text-lg` |
| Arbitrary value | `w-[280px]`, `p-[0.6rem]` (`_` = space, `\_` = literal `_`) |
| CSS variable | `w-(--panel)` |
| Arbitrary property | `[mask-type:luminance]`, `[--gap:2rem]` |

## Variants

| Group | Names |
| --- | --- |
| Breakpoints | `sm` `md` `lg` `xl` `2xl` and `max-*` (40/48/64/80/96rem); extra theme breakpoints included |
| Pseudo-classes | `hover` `focus` `focus-visible` `focus-within` `active` `disabled` `checked` `enabled` `visited` `target` `open` `empty` `read-only` `read-write` `placeholder-shown` `autofill` `invalid` `valid` `user-invalid` `user-valid` `required` `optional` `in-range` `out-of-range` `indeterminate` `default` `inert` |
| Pseudo-elements | `before` `after` `placeholder` `file` `marker` `selection` `first-line` `first-letter` `backdrop` |
| States | `first` `last` `odd` `even` `nth-{n}` `nth-last-{n}` `nth-[...]` `details-content:` |
| Negation | `not-{state}`, `not-[...]` |
| Dark | `dark` - selector prefix (default `.dark`) or media strategy, set in khamsin.json |
| Direction | `rtl` `ltr` |
| Group / peer | `group-*`, `peer-*` for hover/focus/focus-visible/focus-within/active/disabled/checked/first/last/odd/even, with named forms `group-{state}/{name}:`, `peer-{state}/{name}:` |
| Attributes | `data-[state=open]:`, `aria-[sort=ascending]:`, shorthands `data-open:`, `aria-busy:` |
| Arbitrary | `[&>li]:`, `[.is-open_&]:`, `[@media(...)]:`, `[@supports(...)]:`, `[@container(...)]:` |
| Shorthand at-rules | `supports-[display:grid]:`, `min-[600px]:`, `max-[600px]:`, `starting:` |
| Containers | `@sm:` `@2xl:` `@max-lg:` ... against theme container tokens; named `@sm/{name}:` with `@container/{name}`; arbitrary `@min-[475px]:`/`@max-[500px]:` |
| Media | `print` `motion-safe` `motion-reduce` `contrast-more` `contrast-less` `portrait` `landscape` `forced-colors` `pointer-fine` `pointer-coarse` `any-pointer-*` `noscript` `inverted-colors` |

`group`/`peer`/`group/{name}` are plain sentinel classes on the parent element (no CSS needed).

## Utilities

Important: leading `!p-2` (v4) or trailing `p-2!` (v3) - both accepted.

Spacing scale: `0`, `0.5`, `1`-`12`, then `14`-`96`. Colors: 11 built-in ramp families (`ink`
`sand` `ocean` `teal` `forest` `amber` `ember` `rose` `violet` `lime`, 50-950) plus `transparent`
`current` `white` `black` - every color utility takes tokens, arbitrary values, CSS variables and
`/opacity`.

| Family | Utilities |
| --- | --- |
| Layout | `block` `inline*` `flex` `grid` `contents` `table` `flow-root` `hidden` / `sr-only` `not-sr-only` / `static` `fixed` `absolute` `relative` `sticky` / `box-border/content` / `float-*` `clear-*` |
| Inset | `inset` `inset-x/y` `top` `right` `bottom` `left` (spacing, fraction, `auto`, `full`, negative) / `z-{0-50,auto}` |
| Flex | `flex-row/col[-reverse]` `flex-wrap[-reverse]` `flex-nowrap` / `flex` `grow` `shrink` `basis` / `items-*` `justify-*` `content-*` `self-*` `place-*` / `order-*` |
| Grid | `grid-cols-*` `grid-rows-*` (+`subgrid`) / `grid-flow-*` / `col/row-{auto,span-*,start-*,end-*}` / `auto-cols/rows-*` |
| Gap & space | `gap` `gap-x/y` / `space-x/y` (targets `> * + *`) |
| Padding / margin | `p` `px` `py` `pt` `pr` `pb` `pl` / `m` `mx` `my` `mt` `mr` `mb` `ml` (+`auto`, negative) |
| Divide | `divide-x/y` (+widths) `divide-{color}` |
| Sizing | `w` `h` `size` `min-w` `min-h` `max-w` `max-h` - fractions, keywords (`auto` `full` `screen` `min` `max` `fit`; height also `dvh` `svh` `lvh`; `max-w` also container tokens) |
| Logical | `ms/me` `ps/pe` `start/end` (inset) `border-s/e` (+colors) `rounded-s/e` - flow-relative siblings on the same group chains (`px-8` then `ps-4` refines start only) |
| Typography | `text-{size}` / `font-{sans,serif,mono,+config}` `font-{weight}` / `leading-*` `tracking-*` `indent-*` / `text-{align}` / `uppercase` `lowercase` `capitalize` `normal-case` `italic` `not-italic` / `underline` `overline` `line-through` `no-underline` / `decoration-{color,thickness,style}` `underline-offset-*` / `truncate` `text-ellipsis/clip` / `whitespace-*` `break-*` `text-wrap/nowrap/balance/pretty` / `list-*` / `align-*` / `tabular-nums` and the numeric variants / `line-clamp-*` / `text-shadow-*` (+color) / `hyphens-*` `font-stretch-*` `antialiased` `list-image-*` |
| Colors | `bg` `text` `border` `border-{x,y,t,r,b,l}` `ring` `outline` `divide` `fill` `stroke` `accent` `caret` `from` `via` `to` `decoration` - all with `/opacity` |
| Backgrounds | `bg-none` `bg-radial` `bg-conic` `bg-linear-{angle}` / `bg-fixed/local/scroll` / `bg-clip-*` `bg-origin-*` / `bg-{position}` `bg-repeat-*` `bg-auto/cover/contain` / `object-{fit,position}` / `bg-gradient-to-{t,tr,r,br,b,bl,l,tl}` and `bg-linear-to-*` with `from-* via-* to-*` (+`from-10%` stop positions) / `bg-[url(...)]`, `bg-[image/length/position/color:...]` |
| Borders | `border` + widths `0 2 4 8` per side / border colors / `border-{solid,dashed,dotted,double,none}` / `rounded-*` + per-side and corner |
| Effects | `shadow-{sm,md,lg,xl,2xl,inner,none}` + `shadow-{color}` / `inset-shadow-*` (+color) / `opacity-*` / `ring` `ring-{width}` `ring-{color}` `ring-inset` `inset-ring-*` / `outline` + widths, styles, `outline-offset-*`, `outline-hidden` |
| Filters | `blur` `brightness` `contrast` `grayscale` `hue-rotate` `invert` `saturate` `sepia` `drop-shadow` (+color) - all compose; full `backdrop-*` set |
| Masks | `mask-{t,r,b,l,x,y}-from/to-*` / `mask-radial-from/to` `mask-circle/ellipse` `mask-{closest,farthest}-{side,corner}` `mask-radial-at-*` / `mask-linear-{angle}` `mask-linear-from/to` / `mask-conic-{angle,from,to}` / `mask-clip-*` `mask-composite-*` `mask-mode-*` `mask-none` `mask-[...]` |
| Transforms | `translate-x/y/z` / `scale(-x/-y)` / `rotate` `rotate-x/y/z` / `skew-x/y` / `origin-*` `transform-none` `transform-3d/flat` / `perspective-*` `perspective-origin-*` / `backface-visible/hidden` |
| Transitions | `transition-*` / `duration-*` `delay-*` (steps + arbitrary) / `ease-*` (tokens + arbitrary) |
| Animations | `animate-{spin,ping,pulse,bounce}` + `animate-{name}` from khamsin.json (keyframes tree-shaken) / `animate-in/out` with `fade-in/out-*` `zoom-in/out-*` `slide-in/from-*` composables |
| Interactivity | `cursor-*` `select-*` `pointer-events-*` `resize-*` `appearance-*` `accent-*` `caret-*` / `overscroll-*` `touch-*` `will-change-*` `scroll-smooth` / `field-sizing-*` / `forced-color-adjust-*` `scheme-light/dark` |
| Scroll & snap | `scroll-m*` (all sides, negative) `scroll-p*` (all sides) / `snap-{x,y,both,none}` `snap-{mandatory,proximity}` `snap-{start,end,center,align-none}` |
| Tables & misc | `table-auto/fixed` `border-collapse/separate` `caption-top/bottom` `empty-cells-*` / `isolate` `isolation-auto` / `mix-blend-*` `bg-blend-*` / `columns-*` `break-before/after/inside-*` / `aspect-*` / `@container` for container-type |

## Theme tokens

Tokens are emitted as custom properties in `@layer theme` (`--color-*`, `--spacing`, `--radius-*`,
`--shadow-*`, `--text-*`, `--font-*`, `--ease-*`, `--breakpoint-*`, `--container-*`). Override them
in your own `:root` rule after the theme layer to re-theme without changing class names - see
[config.md](config.md).
