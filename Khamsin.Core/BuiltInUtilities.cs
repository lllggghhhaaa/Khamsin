namespace Khamsin;

/// <summary>
/// The declarative utility table. Adding a utility is one entry; nothing else in the engine changes.
/// Order numbers establish the cascade: layout first, then box model, then typography, paint and
/// interaction.
/// </summary>
internal static class BuiltInUtilities
{
    private static readonly StringComparer Ordinal = StringComparer.Ordinal;

    internal static List<UtilityEntry> Create(Theme theme)
    {
        var entries = new List<UtilityEntry>(420)
        {
            // Layout and display
            Static("block", "display:block", "display", 10),
            Static("inline-block", "display:inline-block", "display", 11),
            Static("inline", "display:inline", "display", 12),
            Static("flex", "display:flex", "display", 13),
            Static("inline-flex", "display:inline-flex", "display", 14),
            Static("grid", "display:grid", "display", 15),
            Static("inline-grid", "display:inline-grid", "display", 16),
            Static("contents", "display:contents", "display", 17),
            Static("table", "display:table", "display", 18),
            Static("flow-root", "display:flow-root", "display", 19),
            Static("hidden", "display:none", "display", 20),
            Static("sr-only", "position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border-width:0", "visibility", 30),
            Static("not-sr-only", "position:static;width:auto;height:auto;padding:0;margin:0;overflow:visible;clip:auto;white-space:normal", "visibility", 31),
            Static("static", "position:static", "position", 50),
            Static("fixed", "position:fixed", "position", 51),
            Static("absolute", "position:absolute", "position", 52),
            Static("relative", "position:relative", "position", 53),
            Static("sticky", "position:sticky", "position", 54),
            Static("box-border", "box-sizing:border-box", "box-sizing", 60),
            Static("box-content", "box-sizing:content-box", "box-sizing", 61),

            // Overflow
            Enum("overflow", "overflow", 70, "overflow:{v}", ("auto", "overflow:auto"), ("hidden", "overflow:hidden"), ("visible", "overflow:visible"), ("scroll", "overflow:scroll"), ("clip", "overflow:clip")),
            Enum("overflow-x", "overflow-x", 71, "overflow-x:{v}", ("auto", "overflow-x:auto"), ("hidden", "overflow-x:hidden"), ("visible", "overflow-x:visible"), ("scroll", "overflow-x:scroll"), ("clip", "overflow-x:clip")),
            Enum("overflow-y", "overflow-y", 72, "overflow-y:{v}", ("auto", "overflow-y:auto"), ("hidden", "overflow-y:hidden"), ("visible", "overflow-y:visible"), ("scroll", "overflow-y:scroll"), ("clip", "overflow-y:clip")),
            Static("scroll-smooth", "scroll-behavior:smooth", "scroll-behavior", 73),
            Static("scroll-auto", "scroll-behavior:auto", "scroll-behavior", 74),
            Enum("object", "object-fit", 80, "object-fit:{v}", ("contain", "object-fit:contain"), ("cover", "object-fit:cover"), ("fill", "object-fit:fill"), ("none", "object-fit:none"), ("scale-down", "object-fit:scale-down")),
            Enum("aspect", "aspect", 85, "aspect-ratio:{v}", ("square", "aspect-ratio:1/1"), ("video", "aspect-ratio:16/9")),

            // Position and inset
            Spacing("inset", "inset", "inset", 100, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("inset-x", "inset-inline", "inset/x", 101, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("inset-y", "inset-block", "inset/y", 102, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("top", "top", "inset/top", 103, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("right", "right", "inset/right", 104, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("bottom", "bottom", "inset/bottom", 105, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("left", "left", "inset/left", 106, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("start", "inset-inline-start", "inset/x/start", 107, negative: true, fraction: true, keywords: InsetKeywords()),
            Spacing("end", "inset-inline-end", "inset/x/end", 108, negative: true, fraction: true, keywords: InsetKeywords()),
            Enum("z", "z", 110, "z-index:{v}", ("0", "z-index:0"), ("10", "z-index:10"), ("20", "z-index:20"), ("30", "z-index:30"), ("40", "z-index:40"), ("50", "z-index:50"), ("auto", "z-index:auto")),

            // Container queries: @container names the nearest container (@container/{name} names one).
            new UtilityEntry("@container", UtilityKind.Static, "container", 46,
                template: "container-type:inline-size;container-name:{v}",
                values: new Dictionary<string, string>(Ordinal) { [string.Empty] = "container-type:inline-size" },
                acceptsLabel: true),
            new UtilityEntry("@container/", UtilityKind.Static, "container", 46,
                template: "container-type:inline-size;container-name:{v}",
                acceptsLabel: true),

            // Float and clear
            Enum("float", "float", 45, "float:{v}", ("left", "float:left"), ("right", "float:right"), ("none", "float:none"), ("start", "float:inline-start"), ("end", "float:inline-end")),
            Enum("clear", "clear", 55, "clear:{v}", ("left", "clear:left"), ("right", "clear:right"), ("both", "clear:both"), ("none", "clear:none"), ("start", "clear:inline-start"), ("end", "clear:inline-end")),

            // Flexbox
            Static("flex-row", "flex-direction:row", "flex-direction", 200),
            Static("flex-row-reverse", "flex-direction:row-reverse", "flex-direction", 201),
            Static("flex-col", "flex-direction:column", "flex-direction", 202),
            Static("flex-col-reverse", "flex-direction:column-reverse", "flex-direction", 203),
            Static("flex-wrap", "flex-wrap:wrap", "flex-wrap", 210),
            Static("flex-wrap-reverse", "flex-wrap:wrap-reverse", "flex-wrap", 211),
            Static("flex-nowrap", "flex-wrap:nowrap", "flex-wrap", 212),
            Enum("flex", "flex", 220, "flex:{v}", ("1", "flex:1 1 0%"), ("auto", "flex:1 1 auto"), ("initial", "flex:0 1 auto"), ("none", "flex:none")),
            Enum("grow", "flex-grow", 230, "flex-grow:{v}", ("", "flex-grow:1"), ("0", "flex-grow:0")),
            Enum("shrink", "flex-shrink", 240, "flex-shrink:{v}", ("", "flex-shrink:1"), ("0", "flex-shrink:0")),
            Spacing("basis", "flex-basis", "flex-basis", 250, fraction: true, keywords: Keywords(("auto", "auto"), ("full", "100%"))),
            Enum("items", "align-items", 260, "align-items:{v}", ("start", "align-items:flex-start"), ("end", "align-items:flex-end"), ("center", "align-items:center"), ("baseline", "align-items:baseline"), ("stretch", "align-items:stretch")),
            Enum("justify", "justify-content", 270, "justify-content:{v}", ("start", "justify-content:flex-start"), ("end", "justify-content:flex-end"), ("center", "justify-content:center"), ("between", "justify-content:space-between"), ("around", "justify-content:space-around"), ("evenly", "justify-content:space-evenly"), ("stretch", "justify-content:stretch")),
            new UtilityEntry("content", UtilityKind.Enum, "content", 270, template: "content:{v}", values: ToDictionary(("none", "content:none")), arbitraryTemplate: "--khamsin-content:{v};content:var(--khamsin-content)"),
            Enum("content", "align-content", 280, "align-content:{v}", ("start", "align-content:flex-start"), ("end", "align-content:flex-end"), ("center", "align-content:center"), ("between", "align-content:space-between"), ("around", "align-content:space-around"), ("evenly", "align-content:space-evenly"), ("baseline", "align-content:baseline"), ("stretch", "align-content:stretch")),
            Enum("self", "align-self", 290, "align-self:{v}", ("auto", "align-self:auto"), ("start", "align-self:flex-start"), ("end", "align-self:flex-end"), ("center", "align-self:center"), ("stretch", "align-self:stretch"), ("baseline", "align-self:baseline")),
            Enum("order", "order", 300, "order:{v}", BuildOrderValues()),

            // Grid
            GridTrackEntry("grid-cols", "grid-template-columns", 320, 12),
            GridTrackEntry("grid-rows", "grid-template-rows", 321, 6),
            Static("grid-flow-row", "grid-auto-flow:row", "grid-flow", 322),
            Static("grid-flow-col", "grid-auto-flow:column", "grid-flow", 323),
            Static("grid-flow-dense", "grid-auto-flow:dense", "grid-flow", 324),
            Static("grid-flow-row-dense", "grid-auto-flow:row dense", "grid-flow", 325),
            Static("grid-flow-col-dense", "grid-auto-flow:column dense", "grid-flow", 326),
            Static("col-auto", "grid-column:auto", "grid-column", 330),
            Enum("col-span", "grid-column", 331, "grid-column:span {v}/span {v}", BuildSpanValues("grid-column", 12, "full", "grid-column:1/-1")),
            Enum("col-start", "grid-column-start", 332, "grid-column-start:{v}", BuildRangeValues("grid-column-start", 1, 13, "auto", "grid-column-start:auto")),
            Enum("col-end", "grid-column-end", 333, "grid-column-end:{v}", BuildRangeValues("grid-column-end", 1, 13, "auto", "grid-column-end:auto")),
            Static("row-auto", "grid-row:auto", "grid-row", 335),
            Enum("row-span", "grid-row", 336, "grid-row:span {v}/span {v}", BuildSpanValues("grid-row", 12, "full", "grid-row:1/-1")),
            Enum("row-start", "grid-row-start", 337, "grid-row-start:{v}", BuildRangeValues("grid-row-start", 1, 13, "auto", "grid-row-start:auto")),
            Enum("row-end", "grid-row-end", 338, "grid-row-end:{v}", BuildRangeValues("grid-row-end", 1, 13, "auto", "grid-row-end:auto")),

            // Gap
            Spacing("gap", "gap", "gap", 360),
            Spacing("gap-x", "column-gap", "gap/x", 361),
            Spacing("gap-y", "row-gap", "gap/y", 362),

            // Padding
            Spacing("p", "padding", "padding", 400),
            Spacing("px", "padding-inline", "padding/x", 402),
            Spacing("ps", "padding-inline-start", "padding/x/start", 403),
            Spacing("pe", "padding-inline-end", "padding/x/end", 405),
            Spacing("py", "padding-block", "padding/y", 404),
            Spacing("pt", "padding-top", "padding/t", 406),
            Spacing("pr", "padding-right", "padding/r", 408),
            Spacing("pb", "padding-bottom", "padding/b", 410),
            Spacing("pl", "padding-left", "padding/l", 412),

            // Margin
            Spacing("m", "margin", "margin", 420, negative: true, keywords: AutoOnly()),
            Spacing("mx", "margin-inline", "margin/x", 422, negative: true, keywords: AutoOnly()),
            Spacing("ms", "margin-inline-start", "margin/x/start", 423, negative: true, keywords: AutoOnly()),
            Spacing("me", "margin-inline-end", "margin/x/end", 425, negative: true, keywords: AutoOnly()),
            Spacing("my", "margin-block", "margin/y", 424, negative: true, keywords: AutoOnly()),
            Spacing("mt", "margin-top", "margin/t", 426, negative: true, keywords: AutoOnly()),
            Spacing("mr", "margin-right", "margin/r", 428, negative: true, keywords: AutoOnly()),
            Spacing("mb", "margin-bottom", "margin/b", 430, negative: true, keywords: AutoOnly()),
            Spacing("ml", "margin-left", "margin/l", 432, negative: true, keywords: AutoOnly()),
            Spacing("space-x", "margin-inline-start", "space/x", 440, negative: true, childSelector: ">*+*"),
            Spacing("space-y", "margin-block-start", "space/y", 442, negative: true, childSelector: ">*+*"),

            // Scroll margins, scroll padding and scroll snap
            Spacing("scroll-m", "scroll-margin", "scroll-margin", 444, negative: true),
            Spacing("scroll-mx", "scroll-margin-inline", "scroll-margin/x", 445, negative: true),
            Spacing("scroll-my", "scroll-margin-block", "scroll-margin/y", 446, negative: true),
            Spacing("scroll-mt", "scroll-margin-top", "scroll-margin/t", 447, negative: true),
            Spacing("scroll-mr", "scroll-margin-right", "scroll-margin/r", 448, negative: true),
            Spacing("scroll-mb", "scroll-margin-bottom", "scroll-margin/b", 449, negative: true),
            Spacing("scroll-ml", "scroll-margin-left", "scroll-margin/l", 450, negative: true),
            Spacing("scroll-p", "scroll-padding", "scroll-padding", 451),
            Spacing("scroll-px", "scroll-padding-inline", "scroll-padding/x", 452),
            Spacing("scroll-py", "scroll-padding-block", "scroll-padding/y", 453),
            Spacing("scroll-pt", "scroll-padding-top", "scroll-padding/t", 454),
            Spacing("scroll-pr", "scroll-padding-right", "scroll-padding/r", 455),
            Spacing("scroll-pb", "scroll-padding-bottom", "scroll-padding/b", 456),
            Spacing("scroll-pl", "scroll-padding-left", "scroll-padding/l", 457),
            Enum("snap", "scroll-snap-type", 458, "scroll-snap-type:{v} var(--khamsin-snap-strictness,proximity)",
                ("x", "scroll-snap-type:x var(--khamsin-snap-strictness,proximity)"),
                ("y", "scroll-snap-type:y var(--khamsin-snap-strictness,proximity)"),
                ("both", "scroll-snap-type:both var(--khamsin-snap-strictness,proximity)")),
            Enum("snap", "scroll-snap-type", 459, "scroll-snap-type:{v}", ("none", "scroll-snap-type:none")),
            new UtilityEntry("snap", UtilityKind.Enum, "snap-strictness", 460, values: ToDictionary(
                ("mandatory", "--khamsin-snap-strictness:mandatory"), ("proximity", "--khamsin-snap-strictness:proximity"))),
            Enum("snap", "scroll-snap-align", 461, "scroll-snap-align:{v}",
                ("start", "scroll-snap-align:start"), ("end", "scroll-snap-align:end"),
                ("center", "scroll-snap-align:center"), ("align-none", "scroll-snap-align:none")),

            // Sizing
            Spacing("w", "width", "size/w", 500, fraction: true, keywords: WidthKeywords()),
            Spacing("h", "height", "size/h", 502, fraction: true, keywords: HeightKeywords()),
            Spacing("size", "width", "size", 504, fraction: true, keywords: SizeKeywords()),
            Spacing("min-w", "min-width", "min-size/w", 510, keywords: MinKeywords()),
            Spacing("min-h", "min-height", "min-size/h", 512, keywords: HeightKeywords()),
            Spacing("max-w", "max-width", "max-size/w", 514, keywords: MaxWidthKeywords(theme)),
            Spacing("max-h", "max-height", "max-size/h", 516, keywords: HeightKeywords()),

            // Typography
            TextSizeEntry(theme),
            FontFamilyEntry(theme),
            Enum("font", "font-weight", 620, "font-weight:{v}", BuildFontWeights()),
            Enum("font-stretch", "font-stretch", 621, "font-stretch:{v}%",
                ("50", "font-stretch:50%"), ("75", "font-stretch:75%"), ("100", "font-stretch:100%"), ("125", "font-stretch:125%"),
                ("150", "font-stretch:150%"), ("175", "font-stretch:175%"), ("200", "font-stretch:200%")),
            Static("antialiased", "-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale", "font-smoothing", 612),
            Static("subpixel-antialiased", "-webkit-font-smoothing:auto;-moz-osx-font-smoothing:auto", "font-smoothing", 613),
            Enum("leading", "line-height", 630, "line-height:{v}", ("none", "line-height:1"), ("tight", "line-height:1.25"), ("snug", "line-height:1.375"), ("normal", "line-height:1.5"), ("relaxed", "line-height:1.625"), ("loose", "line-height:2")),
            Enum("tracking", "letter-spacing", 640, "letter-spacing:{v}", ("tighter", "letter-spacing:-0.05em"), ("tight", "letter-spacing:-0.025em"), ("normal", "letter-spacing:0"), ("wide", "letter-spacing:0.025em"), ("wider", "letter-spacing:0.05em"), ("widest", "letter-spacing:0.1em")),
            Enum("text", "text-align", 650, "text-align:{v}", ("left", "text-align:left"), ("center", "text-align:center"), ("right", "text-align:right"), ("justify", "text-align:justify"), ("start", "text-align:start"), ("end", "text-align:end")),
            Static("uppercase", "text-transform:uppercase", "text-transform", 660),
            Static("lowercase", "text-transform:lowercase", "text-transform", 661),
            Static("capitalize", "text-transform:capitalize", "text-transform", 662),
            Static("normal-case", "text-transform:none", "text-transform", 663),
            Static("italic", "font-style:italic", "font-style", 670),
            Static("not-italic", "font-style:normal", "font-style", 671),
            Static("underline", "text-decoration-line:underline", "text-decoration", 680),
            Static("overline", "text-decoration-line:overline", "text-decoration", 681),
            Static("line-through", "text-decoration-line:line-through", "text-decoration", 682),
            Static("no-underline", "text-decoration-line:none", "text-decoration", 683),
            Color("decoration", "text-decoration-color", "text-decoration-color", 684),
            Enum("decoration", "text-decoration-thickness", 685, "text-decoration-thickness:{v}",
                ("auto", "text-decoration-thickness:auto"), ("from-font", "text-decoration-thickness:from-font"),
                ("0", "text-decoration-thickness:0"), ("1", "text-decoration-thickness:1px"),
                ("2", "text-decoration-thickness:2px"), ("4", "text-decoration-thickness:4px"), ("8", "text-decoration-thickness:8px")),
            Enum("decoration", "text-decoration-style", 686, "text-decoration-style:{v}",
                ("solid", "text-decoration-style:solid"), ("double", "text-decoration-style:double"), ("dotted", "text-decoration-style:dotted"),
                ("dashed", "text-decoration-style:dashed"), ("wavy", "text-decoration-style:wavy"), ("none", "text-decoration-style:none")),
            Enum("underline-offset", "text-underline-offset", 687, "text-underline-offset:{v}",
                ("auto", "text-underline-offset:auto"), ("0", "text-underline-offset:0"), ("1", "text-underline-offset:1px"),
                ("2", "text-underline-offset:2px"), ("4", "text-underline-offset:4px"), ("8", "text-underline-offset:8px")),
            Static("truncate", "overflow:hidden;text-overflow:ellipsis;white-space:nowrap", "text-overflow", 690),
            Static("text-ellipsis", "text-overflow:ellipsis", "text-overflow", 691),
            Static("text-clip", "text-overflow:clip", "text-overflow", 692),
            Enum("whitespace", "white-space", 695, "white-space:{v}", ("normal", "white-space:normal"), ("nowrap", "white-space:nowrap"), ("pre", "white-space:pre"), ("pre-line", "white-space:pre-line"), ("pre-wrap", "white-space:pre-wrap"), ("break-spaces", "white-space:break-spaces")),
            Enum("break", "word-break", 697, "overflow-wrap:{v}", ("normal", "overflow-wrap:normal;word-break:normal"), ("words", "overflow-wrap:break-word"), ("all", "word-break:break-all"), ("keep", "word-break:keep-all")),
            Enum("list", "list-style", 699, "list-style-type:{v}", ("none", "list-style-type:none"), ("disc", "list-style-type:disc"), ("decimal", "list-style-type:decimal"), ("inside", "list-style-position:inside"), ("outside", "list-style-position:outside")),
            Static("list-image-none", "list-style-image:none", "list-style-image", 700),
            new UtilityEntry("list-image", UtilityKind.Enum, "list-style-image", 701, arbitraryTemplate: "list-style-image:{v}"),
            Enum("hyphens", "hyphens", 696, "hyphens:{v}", ("none", "hyphens:none"), ("manual", "hyphens:manual"), ("auto", "hyphens:auto")),
            Enum("align", "vertical-align", 705, "vertical-align:{v}", ("baseline", "vertical-align:baseline"), ("top", "vertical-align:top"), ("middle", "vertical-align:middle"), ("bottom", "vertical-align:bottom"), ("text-top", "vertical-align:text-top"), ("text-bottom", "vertical-align:text-bottom")),

            // Colors
            Color("bg", "background-color", "bg-color", 700),
            Color("text", "color", "text-color", 710),
            Color("border", "border-color", "border-color", 720),
            Color("border-x", "border-inline-color", "border-color/x", 721),
            Color("border-y", "border-block-color", "border-color/y", 722),
            Color("border-t", "border-top-color", "border-color/t", 723),
            Color("border-r", "border-right-color", "border-color/r", 724),
            Color("border-b", "border-bottom-color", "border-color/b", 725),
            Color("border-l", "border-left-color", "border-color/l", 726),
            Color("border-s", "border-inline-start-color", "border-color/s", 727),
            Color("border-e", "border-inline-end-color", "border-color/e", 728),
            Color("ring", "--khamsin-ring-color", "ring-color", 730),
            Color("outline", "outline-color", "outline-color", 735),
            Color("fill", "fill", "fill", 750),
            Color("stroke", "stroke", "stroke", 760),
            Color("accent", "accent-color", "accent", 770),
            Color("caret", "caret-color", "caret", 780),

            // Borders
            Static("border", "border-width:1px;border-style:solid", "border-width", 800),
            Enum("border", "border-width", 801, "border-width:{v};border-style:solid", ("0", "border-width:0"), ("2", "border-width:2px;border-style:solid"), ("4", "border-width:4px;border-style:solid"), ("8", "border-width:8px;border-style:solid")),
            Static("border-x", "border-inline-width:1px;border-style:solid", "border-width/x", 802),
            Enum("border-x", "border-inline-width", 803, "border-inline-width:{v};border-style:solid", ("0", "border-inline-width:0"), ("2", "border-inline-width:2px"), ("4", "border-inline-width:4px"), ("8", "border-inline-width:8px")),
            Static("border-y", "border-block-width:1px;border-style:solid", "border-width/y", 804),
            Enum("border-y", "border-block-width", 805, "border-block-width:{v};border-style:solid", ("0", "border-block-width:0"), ("2", "border-block-width:2px"), ("4", "border-block-width:4px"), ("8", "border-block-width:8px")),
            Static("border-t", "border-top-width:1px;border-style:solid", "border-width/t", 806),
            Static("border-r", "border-right-width:1px;border-style:solid", "border-width/r", 807),
            Static("border-b", "border-bottom-width:1px;border-style:solid", "border-width/b", 808),
            Static("border-l", "border-left-width:1px;border-style:solid", "border-width/l", 809),
            Enum("border-t", "border-width/t", 816, "border-top-width:{v};border-style:solid", SideWidthValues("border-top-width")),
            Enum("border-r", "border-width/r", 817, "border-right-width:{v};border-style:solid", SideWidthValues("border-right-width")),
            Enum("border-b", "border-width/b", 818, "border-bottom-width:{v};border-style:solid", SideWidthValues("border-bottom-width")),
            Enum("border-l", "border-width/l", 819, "border-left-width:{v};border-style:solid", SideWidthValues("border-left-width")),
            Static("border-s", "border-inline-start-width:1px;border-style:solid", "border-width/s", 812),
            Enum("border-s", "border-width/s", 822, "border-inline-start-width:{v};border-style:solid", SideWidthValues("border-inline-start-width")),
            Static("border-e", "border-inline-end-width:1px;border-style:solid", "border-width/e", 813),
            Enum("border-e", "border-width/e", 823, "border-inline-end-width:{v};border-style:solid", SideWidthValues("border-inline-end-width")),
            Enum("border", "border-style", 810, "border-style:{v}", ("solid", "border-style:solid"), ("dashed", "border-style:dashed"), ("dotted", "border-style:dotted"), ("double", "border-style:double"), ("none", "border-style:none")),

            // Radius
            TokenEnum("rounded", "radius", 850, "border-radius", "radius", theme.Radius, "border-radius:{v}"),
            MultiTokenEnum("rounded-t", "radius/t", 852, "border-start-start-radius,border-start-end-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-r", "radius/r", 853, "border-start-end-radius,border-end-end-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-b", "radius/b", 854, "border-end-start-radius,border-end-end-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-l", "radius/l", 855, "border-start-start-radius,border-end-start-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-tl", "radius/tl", 856, "border-start-start-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-tr", "radius/tr", 857, "border-start-end-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-br", "radius/br", 858, "border-end-end-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-bl", "radius/bl", 859, "border-end-start-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-s", "radius/s", 860, "border-start-start-radius,border-end-start-radius", "radius", theme.Radius),
            MultiTokenEnum("rounded-e", "radius/e", 861, "border-start-end-radius,border-end-end-radius", "radius", theme.Radius),

            // Effects
            ShadowEntry(theme),
            new UtilityEntry("shadow", UtilityKind.Color, "shadow-color", 901,
                template: "--khamsin-shadow-color:{v};box-shadow:var(--khamsin-shadow-colored,0 0 #0000)"),
            Enum("opacity", "opacity", 910, "opacity:{v}%", BuildOpacityValues()),
            Static("ring", "box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-offset-width,0px) var(--khamsin-ring-offset-color,#fff), var(--khamsin-ring-inset,) 0 0 0 3px var(--khamsin-ring-color,currentColor)", "ring-width", 920),
            RingWidthEntry(),
            Static("ring-inset", "--khamsin-ring-inset:inset", "ring-inset", 925),
            Static("outline", "outline-style:solid;outline-width:2px", "outline-width", 930),
            Static("outline-hidden", "outline:2px solid transparent;outline-offset:2px", "outline-width", 934),
            OutlineWidthEntry(),
            Enum("outline", "outline-style", 933, "outline-style:{v}", ("solid", "outline-style:solid"), ("dashed", "outline-style:dashed"), ("dotted", "outline-style:dotted"), ("double", "outline-style:double"), ("none", "outline-style:none")),
            Enum("outline-offset", "outline-offset", 940, "outline-offset:{v}", ("0", "outline-offset:0"), ("1", "outline-offset:1px"), ("2", "outline-offset:2px"), ("4", "outline-offset:4px"), ("8", "outline-offset:8px")),

            // Transitions
            Static("transition", "transition-property:color,background-color,border-color,outline-color,text-decoration-color,fill,stroke,opacity,box-shadow,transform;transition-timing-function:var(--ease-out);transition-duration:150ms", "transition", 950),
            Static("transition-all", "transition-property:all;transition-timing-function:var(--ease-out);transition-duration:150ms", "transition", 951),
            Static("transition-colors", "transition-property:color,background-color,border-color,outline-color,text-decoration-color,fill,stroke;transition-timing-function:var(--ease-out);transition-duration:150ms", "transition", 952),
            Static("transition-opacity", "transition-property:opacity;transition-timing-function:var(--ease-out);transition-duration:150ms", "transition", 953),
            Static("transition-transform", "transition-property:transform;transition-timing-function:var(--ease-out);transition-duration:150ms", "transition", 954),
            Static("transition-none", "transition-property:none", "transition", 955),
            new UtilityEntry("duration", UtilityKind.Enum, "duration", 960, template: "transition-duration:{v}ms", values: ToDictionary(("75", "transition-duration:75ms"), ("100", "transition-duration:100ms"), ("150", "transition-duration:150ms"), ("200", "transition-duration:200ms"), ("300", "transition-duration:300ms"), ("500", "transition-duration:500ms"), ("700", "transition-duration:700ms"), ("1000", "transition-duration:1000ms")), arbitraryTemplate: "transition-duration:{v}"),
            TokenEnum("ease", "ease", 965, "transition-timing-function", "ease", theme.Eases, "transition-timing-function:{v}"),
            new UtilityEntry("delay", UtilityKind.Enum, "delay", 970, template: "transition-delay:{v}ms", values: ToDictionary(("75", "transition-delay:75ms"), ("100", "transition-delay:100ms"), ("150", "transition-delay:150ms"), ("200", "transition-delay:200ms"), ("300", "transition-delay:300ms"), ("500", "transition-delay:500ms"), ("700", "transition-delay:700ms"), ("1000", "transition-delay:1000ms")), arbitraryTemplate: "transition-delay:{v}"),

            // Interactivity
            Enum("cursor", "cursor", 1000, "cursor:{v}", ("auto", "cursor:auto"), ("default", "cursor:default"), ("pointer", "cursor:pointer"), ("wait", "cursor:wait"), ("text", "cursor:text"), ("move", "cursor:move"), ("help", "cursor:help"), ("not-allowed", "cursor:not-allowed"), ("none", "cursor:none"), ("grab", "cursor:grab"), ("grabbing", "cursor:grabbing"), ("zoom-in", "cursor:zoom-in"), ("zoom-out", "cursor:zoom-out"), ("crosshair", "cursor:crosshair"), ("progress", "cursor:progress")),
            Enum("select", "select", 1010, "user-select:{v}", ("none", "user-select:none"), ("text", "user-select:text"), ("all", "user-select:all"), ("auto", "user-select:auto")),
            Enum("pointer-events", "pointer-events", 1020, "pointer-events:{v}", ("none", "pointer-events:none"), ("auto", "pointer-events:auto")),
            Enum("resize", "resize", 1030, "resize:{v}", ("none", "resize:none"), ("y", "resize:vertical"), ("x", "resize:horizontal"), ("both", "resize:both")),
            Enum("field-sizing", "field-sizing", 1035, "field-sizing:{v}", ("content", "field-sizing:content"), ("fixed", "field-sizing:fixed")),

            // Transforms. Individual properties compose without a transform class.
            new UtilityEntry("translate-x", UtilityKind.Spacing, "translate/x", 560, template: "--khamsin-translate-x:{v};translate:var(--khamsin-translate-x) var(--khamsin-translate-y,0) var(--khamsin-translate-z,0)", allowNegative: true, acceptsFraction: true),
            new UtilityEntry("translate-y", UtilityKind.Spacing, "translate/y", 561, template: "--khamsin-translate-y:{v};translate:var(--khamsin-translate-x,0) var(--khamsin-translate-y) var(--khamsin-translate-z,0)", allowNegative: true, acceptsFraction: true),
            new UtilityEntry("translate-z", UtilityKind.Spacing, "translate/z", 562, template: "--khamsin-translate-z:{v};translate:var(--khamsin-translate-x,0) var(--khamsin-translate-y,0) var(--khamsin-translate-z)"),
            ScaleEntry(),
            ScaleAxisEntry("scale-x", "scale/x", 565, xAxis: true),
            ScaleAxisEntry("scale-y", "scale/y", 566, xAxis: false),
            new UtilityEntry("rotate", UtilityKind.Enum, "rotate", 568, template: "rotate:{v}deg", allowNegative: true, values: ToDictionary(("0", "rotate:0deg"), ("1", "rotate:1deg"), ("2", "rotate:2deg"), ("3", "rotate:3deg"), ("6", "rotate:6deg"), ("12", "rotate:12deg"), ("45", "rotate:45deg"), ("90", "rotate:90deg"), ("180", "rotate:180deg")), arbitraryTemplate: "rotate:{v}"),
            Rotate3dEntry("rotate-x", "rotate/x", "X"),
            Rotate3dEntry("rotate-y", "rotate/y", "Y"),
            Rotate3dEntry("rotate-z", "rotate/z", "Z"),
            new UtilityEntry("skew-x", UtilityKind.Enum, "skew/x", 569, allowNegative: true, template: "transform:skewX({v}deg)", values: ToDictionary(("0", "transform:skewX(0deg)"), ("1", "transform:skewX(1deg)"), ("2", "transform:skewX(2deg)"), ("3", "transform:skewX(3deg)"), ("6", "transform:skewX(6deg)"), ("12", "transform:skewX(12deg)")), arbitraryTemplate: "transform:skewX({v})"),
            new UtilityEntry("skew-y", UtilityKind.Enum, "skew/y", 570, allowNegative: true, template: "transform:skewY({v}deg)", values: ToDictionary(("0", "transform:skewY(0deg)"), ("1", "transform:skewY(1deg)"), ("2", "transform:skewY(2deg)"), ("3", "transform:skewY(3deg)"), ("6", "transform:skewY(6deg)"), ("12", "transform:skewY(12deg)")), arbitraryTemplate: "transform:skewY({v})"),
            Enum("perspective", "perspective", 571, "perspective:{v}",
                ("dramatic", "perspective:100px"), ("near", "perspective:300px"), ("normal", "perspective:500px"), ("midrange", "perspective:800px"), ("distant", "perspective:1200px"), ("none", "perspective:none")),
            Enum("perspective-origin", "perspective-origin", 572, "perspective-origin:{v}",
                ("center", "perspective-origin:center"), ("top", "perspective-origin:top"), ("bottom", "perspective-origin:bottom"), ("left", "perspective-origin:left"), ("right", "perspective-origin:right")),
            Static("transform-3d", "transform-style:preserve-3d", "transform-style", 573),
            Static("transform-flat", "transform-style:flat", "transform-style", 574),
            Static("backface-visible", "backface-visibility:visible", "backface-visibility", 575),
            Static("backface-hidden", "backface-visibility:hidden", "backface-visibility", 576),
            Static("transform-none", "transform:none", "transform", 575),
            Enum("origin", "transform-origin", 576, "transform-origin:{v}", ("center", "transform-origin:center"), ("top", "transform-origin:top"), ("top-right", "transform-origin:top right"), ("right", "transform-origin:right"), ("bottom-right", "transform-origin:bottom right"), ("bottom", "transform-origin:bottom"), ("bottom-left", "transform-origin:bottom left"), ("left", "transform-origin:left"), ("top-left", "transform-origin:top left")),

            // Divide utilities apply to every child but the first.
            Static("divide-x", "border-inline-start-width:1px;border-style:solid", "divide/x", 578, childSelector: ">*+*"),
            new UtilityEntry("divide-x", UtilityKind.Enum, "divide/x", 579, template: "border-inline-start-width:{v};border-style:solid", values: ToDictionary(("0", "border-inline-start-width:0;border-style:solid"), ("2", "border-inline-start-width:2px;border-style:solid"), ("4", "border-inline-start-width:4px;border-style:solid"), ("8", "border-inline-start-width:8px;border-style:solid")), childSelector: ">*+*"),
            Static("divide-y", "border-block-start-width:1px;border-style:solid", "divide/y", 580, childSelector: ">*+*"),
            new UtilityEntry("divide-y", UtilityKind.Enum, "divide/y", 581, template: "border-block-start-width:{v};border-style:solid", values: ToDictionary(("0", "border-block-start-width:0;border-style:solid"), ("2", "border-block-start-width:2px;border-style:solid"), ("4", "border-block-start-width:4px;border-style:solid"), ("8", "border-block-start-width:8px;border-style:solid")), childSelector: ">*+*"),
            Color("divide", "border-color", "divide-color", 585, childSelector: ">*+*"),

            // Line clamping
            new UtilityEntry("line-clamp", UtilityKind.Enum, "line-clamp", 698, template: "display:-webkit-box;overflow:hidden;-webkit-box-orient:vertical;-webkit-line-clamp:{v}", values: LineClampValues(), arbitraryTemplate: "display:-webkit-box;overflow:hidden;-webkit-box-orient:vertical;-webkit-line-clamp:{v}"),

            // Alignment and grid auto tracks
            Enum("place-items", "place-items", 295, "place-items:{v}", ("start", "place-items:start"), ("end", "place-items:end"), ("center", "place-items:center"), ("baseline", "place-items:baseline"), ("stretch", "place-items:stretch")),
            Enum("place-content", "place-content", 296, "place-content:{v}", ("start", "place-content:start"), ("end", "place-content:end"), ("center", "place-content:center"), ("between", "place-content:space-between"), ("around", "place-content:space-around"), ("evenly", "place-content:space-evenly"), ("baseline", "place-content:baseline"), ("stretch", "place-content:stretch")),
            Enum("place-self", "place-self", 297, "place-self:{v}", ("auto", "place-self:auto"), ("start", "place-self:start"), ("end", "place-self:end"), ("center", "place-self:center"), ("stretch", "place-self:stretch")),
            Enum("auto-cols", "grid-auto-columns", 328, "grid-auto-columns:{v}", ("auto", "grid-auto-columns:auto"), ("min", "grid-auto-columns:min-content"), ("max", "grid-auto-columns:max-content"), ("fr", "grid-auto-columns:minmax(0,1fr)")),
            Enum("auto-rows", "grid-auto-rows", 329, "grid-auto-rows:{v}", ("auto", "grid-auto-rows:auto"), ("min", "grid-auto-rows:min-content"), ("max", "grid-auto-rows:max-content"), ("fr", "grid-auto-rows:minmax(0,1fr)")),

            // Backgrounds
            Static("bg-fixed", "background-attachment:fixed", "background-attachment", 820),
            Static("bg-local", "background-attachment:local", "background-attachment", 821),
            Static("bg-scroll", "background-attachment:scroll", "background-attachment", 822),
            Static("bg-clip-border", "background-clip:border-box", "background-clip", 823),
            Static("bg-clip-padding", "background-clip:padding-box", "background-clip", 824),
            Static("bg-clip-content", "background-clip:content-box", "background-clip", 825),
            Static("bg-clip-text", "background-clip:text;-webkit-background-clip:text", "background-clip", 826),
            Static("bg-origin-border", "background-origin:border-box", "background-origin", 827),
            Static("bg-origin-padding", "background-origin:padding-box", "background-origin", 828),
            Static("bg-origin-content", "background-origin:content-box", "background-origin", 829),
            Static("bg-bottom", "background-position:bottom", "background-position", 830),
            Static("bg-center", "background-position:center", "background-position", 831),
            Static("bg-left", "background-position:left", "background-position", 832),
            Static("bg-left-bottom", "background-position:left bottom", "background-position", 833),
            Static("bg-left-top", "background-position:left top", "background-position", 834),
            Static("bg-right", "background-position:right", "background-position", 835),
            Static("bg-right-bottom", "background-position:right bottom", "background-position", 836),
            Static("bg-right-top", "background-position:right top", "background-position", 837),
            Static("bg-top", "background-position:top", "background-position", 838),
            Static("bg-repeat", "background-repeat:repeat", "background-repeat", 840),
            Static("bg-no-repeat", "background-repeat:no-repeat", "background-repeat", 841),
            Static("bg-repeat-x", "background-repeat:repeat-x", "background-repeat", 842),
            Static("bg-repeat-y", "background-repeat:repeat-y", "background-repeat", 843),
            Static("bg-repeat-round", "background-repeat:round", "background-repeat", 844),
            Static("bg-repeat-space", "background-repeat:space", "background-repeat", 845),
            Static("bg-auto", "background-size:auto", "background-size", 846),
            Static("bg-cover", "background-size:cover", "background-size", 847),
            Static("bg-contain", "background-size:contain", "background-size", 848),

            // Gradients: color stops compose through --khamsin-gradient-* like Tailwind.
            GradientDirectionEntry("bg-gradient-to"),
            GradientDirectionEntry("bg-linear-to"),
            new UtilityEntry("from", UtilityKind.Color, "gradient/from", 846,
                template: "--khamsin-gradient-from:{v};--khamsin-gradient-from-position:0%;--khamsin-gradient-to:color-mix(in oklab,{v} 0%,transparent);--khamsin-gradient-to-position:100%;--khamsin-gradient-stops:var(--khamsin-gradient-from) var(--khamsin-gradient-from-position),var(--khamsin-gradient-to) var(--khamsin-gradient-to-position)"),
            new UtilityEntry("via", UtilityKind.Color, "gradient/via", 847,
                template: "--khamsin-gradient-via:{v};--khamsin-gradient-via-position:50%;--khamsin-gradient-stops:var(--khamsin-gradient-from) var(--khamsin-gradient-from-position),var(--khamsin-gradient-via) var(--khamsin-gradient-via-position),var(--khamsin-gradient-to) var(--khamsin-gradient-to-position)"),
            new UtilityEntry("to", UtilityKind.Color, "gradient/to", 848, template: "--khamsin-gradient-to:{v}"),
            GradientStopPositions("from", "--khamsin-gradient-from-position"),
            GradientStopPositions("via", "--khamsin-gradient-via-position"),
            GradientStopPositions("to", "--khamsin-gradient-to-position"),
            Static("bg-none", "background-image:none", "gradient/direction", 850),
            Static("bg-radial", "background-image:radial-gradient(circle,var(--khamsin-gradient-stops,transparent))", "gradient/direction", 850),
            Static("bg-conic", "background-image:conic-gradient(var(--khamsin-gradient-stops,transparent))", "gradient/direction", 850),
            GradientAngleEntry(),
            Enum("object", "object-position", 82, "object-position:{v}", ("bottom", "object-position:bottom"), ("center", "object-position:center"), ("left", "object-position:left"), ("left-bottom", "object-position:left bottom"), ("left-top", "object-position:left top"), ("right", "object-position:right"), ("right-bottom", "object-position:right bottom"), ("right-top", "object-position:right top"), ("top", "object-position:top")),

            // Tables and text wrapping
            Static("table-auto", "table-layout:auto", "table-layout", 850),
            Static("table-fixed", "table-layout:fixed", "table-layout", 851),
            Static("border-collapse", "border-collapse:collapse", "border-collapse", 852),
            Static("border-separate", "border-collapse:separate", "border-collapse", 853),
            Static("caption-top", "caption-side:top", "caption-side", 854),
            Static("caption-bottom", "caption-side:bottom", "caption-side", 855),
            Static("empty-cells-show", "empty-cells:show", "empty-cells", 856),
            Static("empty-cells-hide", "empty-cells:hide", "empty-cells", 857),
            Static("text-wrap", "text-wrap:wrap", "text-wrap", 700),
            Static("text-nowrap", "text-wrap:nowrap", "text-wrap", 701),
            Static("text-balance", "text-wrap:balance", "text-wrap", 702),
            Static("text-pretty", "text-wrap:pretty", "text-wrap", 703),

            // Blending and isolation
            Static("isolate", "isolation:isolate", "isolation", 900),
            Static("isolation-auto", "isolation:auto", "isolation", 901),
            Enum("mix-blend", "mix-blend-mode", 902, "mix-blend-mode:{v}", ("normal", "mix-blend-mode:normal"), ("multiply", "mix-blend-mode:multiply"), ("screen", "mix-blend-mode:screen"), ("overlay", "mix-blend-mode:overlay"), ("darken", "mix-blend-mode:darken"), ("lighten", "mix-blend-mode:lighten"), ("color-dodge", "mix-blend-mode:color-dodge"), ("color-burn", "mix-blend-mode:color-burn"), ("hard-light", "mix-blend-mode:hard-light"), ("soft-light", "mix-blend-mode:soft-light"), ("difference", "mix-blend-mode:difference"), ("exclusion", "mix-blend-mode:exclusion"), ("hue", "mix-blend-mode:hue"), ("saturation", "mix-blend-mode:saturation"), ("color", "mix-blend-mode:color"), ("luminosity", "mix-blend-mode:luminosity"), ("plus-lighter", "mix-blend-mode:plus-lighter")),
            Enum("bg-blend", "background-blend-mode", 903, "background-blend-mode:{v}", ("normal", "background-blend-mode:normal"), ("multiply", "background-blend-mode:multiply"), ("screen", "background-blend-mode:screen"), ("overlay", "background-blend-mode:overlay"), ("darken", "background-blend-mode:darken"), ("lighten", "background-blend-mode:lighten"), ("color-dodge", "background-blend-mode:color-dodge"), ("color-burn", "background-blend-mode:color-burn"), ("hard-light", "background-blend-mode:hard-light"), ("soft-light", "background-blend-mode:soft-light"), ("difference", "background-blend-mode:difference"), ("exclusion", "background-blend-mode:exclusion"), ("hue", "background-blend-mode:hue"), ("saturation", "background-blend-mode:saturation"), ("color", "background-blend-mode:color"), ("luminosity", "background-blend-mode:luminosity")),

            // Interaction details
            Enum("appearance", "appearance", 1012, "appearance:{v}", ("none", "appearance:none"), ("auto", "appearance:auto")),
            Enum("forced-color-adjust", "forced-color-adjust", 1013, "forced-color-adjust:{v}", ("auto", "forced-color-adjust:auto"), ("none", "forced-color-adjust:none")),
            Enum("scheme", "color-scheme", 1014, "color-scheme:{v}", ("light", "color-scheme:light"), ("dark", "color-scheme:dark"), ("normal", "color-scheme:normal")),
            Enum("overscroll", "overscroll-behavior", 1040, "overscroll-behavior:{v}", ("auto", "overscroll-behavior:auto"), ("contain", "overscroll-behavior:contain"), ("none", "overscroll-behavior:none")),
            Enum("overscroll-x", "overscroll-behavior-x", 1041, "overscroll-behavior-x:{v}", ("auto", "overscroll-behavior-x:auto"), ("contain", "overscroll-behavior-x:contain"), ("none", "overscroll-behavior-x:none")),
            Enum("overscroll-y", "overscroll-behavior-y", 1042, "overscroll-behavior-y:{v}", ("auto", "overscroll-behavior-y:auto"), ("contain", "overscroll-behavior-y:contain"), ("none", "overscroll-behavior-y:none")),
            Enum("touch", "touch-action", 1050, "touch-action:{v}", ("auto", "touch-action:auto"), ("none", "touch-action:none"), ("pan-x", "touch-action:pan-x"), ("pan-y", "touch-action:pan-y"), ("pan-left", "touch-action:pan-left"), ("pan-right", "touch-action:pan-right"), ("pan-up", "touch-action:pan-up"), ("pan-down", "touch-action:pan-down"), ("manipulation", "touch-action:manipulation"), ("pinch-zoom", "touch-action:pinch-zoom")),
            Enum("will-change", "will-change", 1060, "will-change:{v}", ("auto", "will-change:auto"), ("scroll", "will-change:scroll-position"), ("contents", "will-change:contents"), ("transform", "will-change:transform")),

            // Columns, breaks, SVG paint and text indent
            new UtilityEntry("columns", UtilityKind.Enum, "columns", 95, template: "columns:{v}", values: ColumnValues()),
            Enum("break-before", "break-before", 90, "break-before:{v}", ("auto", "break-before:auto"), ("avoid", "break-before:avoid"), ("all", "break-before:all"), ("avoid-page", "break-before:avoid-page"), ("page", "break-before:page"), ("left", "break-before:left"), ("right", "break-before:right"), ("column", "break-before:column")),
            Enum("break-after", "break-after", 91, "break-after:{v}", ("auto", "break-after:auto"), ("avoid", "break-after:avoid"), ("all", "break-after:all"), ("avoid-page", "break-after:avoid-page"), ("page", "break-after:page"), ("left", "break-after:left"), ("right", "break-after:right"), ("column", "break-after:column")),
            Enum("break-inside", "break-inside", 92, "break-inside:{v}", ("auto", "break-inside:auto"), ("avoid", "break-inside:avoid"), ("avoid-page", "break-inside:avoid-page"), ("avoid-column", "break-inside:avoid-column")),
            Enum("stroke", "stroke-width", 765, "stroke-width:{v}", ("0", "stroke-width:0"), ("1", "stroke-width:1"), ("2", "stroke-width:2")),
            Static("fill-none", "fill:none", "fill", 766),
            Spacing("indent", "text-indent", "indent", 600, negative: true, fraction: true),
        };

        // Animations: theme-registered keyframe animations plus the enter/exit composition set.
        entries.AddRange(AnimationEntries(theme));

        // Filters and backdrop filters compose through --khamsin-* variables.
        entries.AddRange(FilterEntries());

        // Font variant numeric; the slots compose like filters.
        entries.AddRange(NumericEntries());

        // Masks compose edge/radial/linear/conic layers through --khamsin-mask-* variables.
        entries.AddRange(MaskEntries());

        // v4 inset ring, inset and text shadows with their color companions.
        entries.AddRange(InsetRingEntry());
        entries.AddRange(InsetShadowEntries());
        entries.AddRange(TextShadowEntries());
        entries.AddRange(RingOffsetEntries());

        return entries;
    }

    /// <summary>Grid track utilities accept 1..N plus the v4 subgrid value.</summary>
    private static UtilityEntry GridTrackEntry(string prefix, string property, int order, int count)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var (value, declarations) in BuildRepeatValues(property, count))
            values[value] = declarations;

        values["subgrid"] = property + ":subgrid";
        return new UtilityEntry(prefix, UtilityKind.Enum, prefix, order, template: property + ":repeat({v},minmax(0,1fr))", values: values);
    }

    /// <summary>3D axis rotations share the 2D degree scale and use the individual rotate property with an axis.</summary>
    private static UtilityEntry Rotate3dEntry(string prefix, string group, string axis)
    {
        var lower = axis.ToLowerInvariant();
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var degrees in new[] { "0", "1", "2", "3", "6", "12", "45", "90", "180" })
            values[degrees] = "rotate:" + lower + " " + degrees + "deg";

        return new UtilityEntry(prefix, UtilityKind.Enum, group, 568, template: "rotate:" + lower + " {v}deg", allowNegative: true, values: values, arbitraryTemplate: "rotate:" + lower + " {v}deg");
    }

    /// <summary>Bare angle gradients: <c>bg-linear-45</c> points the shared stops at a fixed slope.</summary>
    private static UtilityEntry GradientAngleEntry()
    {
        var values = new Dictionary<string, string>(Ordinal);
        for (var angle = 0; angle <= 360; angle += 15)
        {
            var token = angle.ToString();
            values[token] = "background-image:linear-gradient(" + token + "deg,var(--khamsin-gradient-stops,transparent))";
        }

        return new UtilityEntry(
            "bg-linear",
            UtilityKind.Enum,
            "gradient/direction",
            849,
            template: "background-image:linear-gradient({v}deg,var(--khamsin-gradient-stops,transparent))",
            values: values);
    }

    /// <summary>Gradient directions are fixed declarations over the shared stops variable.</summary>
    private static UtilityEntry GradientDirectionEntry(string prefix)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var (key, direction) in new[]
                 {
                     ("t", "top"), ("tr", "top right"), ("r", "right"), ("br", "bottom right"),
                     ("b", "bottom"), ("bl", "bottom left"), ("l", "left"), ("tl", "top left"),
                 })
        {
            values[key] = "background-image:linear-gradient(to " + direction + ",var(--khamsin-gradient-stops,transparent))";
        }

        return new UtilityEntry(
            prefix,
            UtilityKind.Enum,
            "gradient/direction",
            849,
            template: "background-image:linear-gradient(to {v},var(--khamsin-gradient-stops,transparent))",
            values: values);
    }

    /// <summary>Stop positions (<c>from-10%</c> style) feed the position vars referenced by the stops.</summary>
    private static UtilityEntry GradientStopPositions(string prefix, string variable)
    {
        var values = new Dictionary<string, string>(Ordinal);
        for (var percent = 0; percent <= 100; percent += 5)
        {
            var token = percent + "%";
            values[token] = variable + ":" + token;
        }

        return new UtilityEntry(prefix, UtilityKind.Enum, prefix + "-position", 850, values: values);
    }

    /// <summary>
    /// Shadow tokens carry their color-swapped twin inline so a later <c>shadow-&lt;color&gt;</c>
    /// rule can re-issue <c>box-shadow</c> with the element's shadow color substituted.
    /// </summary>
    private static UtilityEntry ShadowEntry(Theme theme)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var token in theme.Shadows)
        {
            var colored = ShadowColors.DeriveColored(token.Value);
            values[token.Key] = "--khamsin-shadow-colored:" + colored + ";box-shadow:var(--shadow-" + token.Key + ")";
        }

        return new UtilityEntry(
            "shadow",
            UtilityKind.Enum,
            "shadow",
            900,
            template: "box-shadow:{v}",
            values: values,
            arbitraryTemplate: "box-shadow:{v}");
    }

    /// <summary>Animations from the theme, plus animate-in/animate-out for enter/exit composition.</summary>
    private static List<UtilityEntry> AnimationEntries(Theme theme)
    {
        var entries = new List<UtilityEntry>(12);
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var animation in theme.Animations)
        {
            if (animation.Key.StartsWith("khamsin-", StringComparison.Ordinal))
                continue; // Reserved: referenced by animate-in/animate-out, never by name.

            values[animation.Key] = "animation:" + animation.Value.Value;
        }

        values["none"] = "animation:none";
        entries.Add(new UtilityEntry(
            "animate",
            UtilityKind.Enum,
            "animation",
            945,
            template: "animation:{v}",
            values: values,
            arbitraryTemplate: "animation:{v}"));

        entries.Add(new UtilityEntry(
            "animate-in",
            UtilityKind.Enum,
            "animation",
            946,
            template: "animation:{v}",
            values: ToDictionary(("", "animation:khamsin-enter 0.15s var(--ease-out) both")),
            arbitraryTemplate: "animation:{v}"));
        entries.Add(new UtilityEntry(
            "animate-out",
            UtilityKind.Enum,
            "animation",
            947,
            template: "animation:{v}",
            values: ToDictionary(("", "animation:khamsin-exit 0.15s var(--ease-out) both")),
            arbitraryTemplate: "animation:{v}"));

        // fade-in-<opacity>, zoom-in-<percent> and slide-in-from-<edge>-<spacing> set the enter
        // variables consumed by the khamsin-enter keyframes; the -out family mirrors them.
        var fadeIn = new Dictionary<string, string>(Ordinal) { [string.Empty] = "--khamsin-enter-opacity:0" };
        var fadeOut = new Dictionary<string, string>(Ordinal) { [string.Empty] = "--khamsin-exit-opacity:0" };
        for (var opacity = 5; opacity <= 100; opacity += 5)
        {
            fadeIn[opacity.ToString()] = "--khamsin-enter-opacity:" + SpacingScale.FormatNumber(opacity / 100d);
            fadeOut[opacity.ToString()] = "--khamsin-exit-opacity:" + SpacingScale.FormatNumber(opacity / 100d);
        }

        entries.Add(new UtilityEntry("fade-in", UtilityKind.Enum, "enter/opacity", 948, template: "--khamsin-enter-opacity:{v}", values: fadeIn));
        entries.Add(new UtilityEntry("fade-out", UtilityKind.Enum, "exit/opacity", 949, template: "--khamsin-exit-opacity:{v}", values: fadeOut));

        foreach (var (prefix, group, template) in new[]
                 {
                     ("zoom-in", "enter/scale", "--khamsin-enter-scale:{v}"),
                     ("zoom-out", "exit/scale", "--khamsin-exit-scale:{v}"),
                 })
        {
            var zoomValues = new Dictionary<string, string>(Ordinal) { [string.Empty] = template.Replace("{v}", "0") };
            foreach (var step in ScaleSteps)
                zoomValues[step.Key] = template.Replace("{v}", step.Value);

            entries.Add(new UtilityEntry(prefix, UtilityKind.Enum, group, 950, template: template, values: zoomValues));
        }

        foreach (var edge in new[] { "top", "bottom", "left", "right" })
        {
            entries.Add(SlideEntry("slide-in-from-" + edge, "enter/translate-" + (edge is "top" or "bottom" ? "y" : "x"), edge, entering: true));
            entries.Add(SlideEntry("slide-out-to-" + edge, "exit/translate-" + (edge is "top" or "bottom" ? "y" : "x"), edge, entering: false));
        }

        return entries;
    }

    /// <summary>
    /// Slides enter from the named edge and exit toward it: top and left edges carry negative
    /// offsets, bottom and right positive ones.
    /// </summary>
    private static UtilityEntry SlideEntry(string prefix, string group, string edge, bool entering)
    {
        var axis = edge is "top" or "bottom" ? "y" : "x";
        var negative = edge is "top" or "left";
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var key in SpacingScale.Steps)
        {
            if (!SpacingScale.TryFormat(key, negative, out var formatted))
                continue;

            values[key] = "--khamsin-" + (entering ? "enter" : "exit") + "-translate-" + axis + ":" + formatted;
        }

        return new UtilityEntry(
            prefix,
            UtilityKind.Enum,
            group,
            951,
            template: "--khamsin-" + (entering ? "enter" : "exit") + "-translate-" + axis + ":{v}",
            values: values);
    }

    private const string FilterComposition =
        "filter:var(--khamsin-blur,) var(--khamsin-brightness,) var(--khamsin-contrast,) var(--khamsin-grayscale,) var(--khamsin-hue-rotate,) var(--khamsin-invert,) var(--khamsin-saturate,) var(--khamsin-sepia,) var(--khamsin-drop-shadow,)";

    private const string BackdropComposition =
        "backdrop-filter:var(--khamsin-backdrop-blur,) var(--khamsin-backdrop-brightness,) var(--khamsin-backdrop-contrast,) var(--khamsin-backdrop-grayscale,) var(--khamsin-backdrop-hue-rotate,) var(--khamsin-backdrop-invert,) var(--khamsin-backdrop-saturate,) var(--khamsin-backdrop-sepia,)";

    /// <summary>Filter and backdrop-filter families; every utility re-issues the composed property so the pieces stack.</summary>
    private static List<UtilityEntry> FilterEntries()
    {
        var entries = new List<UtilityEntry>(40);

        void Family(string prefix, string group, string variable, string function, string composition, int order,
            params (string Value, string Argument)[] steps)
        {
            var values = new Dictionary<string, string>(Ordinal);
            foreach (var (value, argument) in steps)
                values[value] = "--khamsin-" + variable + ":" + function.Replace("{v}", argument) + ";" + composition;

            entries.Add(new UtilityEntry(
                prefix,
                UtilityKind.Enum,
                group,
                order,
                template: "--khamsin-" + variable + ":" + function + ";" + composition,
                values: values,
                arbitraryTemplate: "--khamsin-" + variable + ":" + function + ";" + composition));
        }

        Family("blur", "filter/blur", "blur", "blur({v})", FilterComposition, 953,
            ("none", "0"), ("sm", "4px"), ("", "8px"), ("md", "12px"), ("lg", "16px"), ("xl", "24px"), ("2xl", "40px"), ("3xl", "64px"));
        Family("brightness", "filter/brightness", "brightness", "brightness({v})", FilterComposition, 954, PercentScales());
        Family("contrast", "filter/contrast", "contrast", "contrast({v})", FilterComposition, 955, PercentScales());
        Family("grayscale", "filter/grayscale", "grayscale", "grayscale({v})", FilterComposition, 956, ("", "100%"), ("0", "0"));
        Family("hue-rotate", "filter/hue-rotate", "hue-rotate", "hue-rotate({v})", FilterComposition, 957,
            ("0", "0deg"), ("15", "15deg"), ("30", "30deg"), ("60", "60deg"), ("90", "90deg"), ("180", "180deg"));
        Family("invert", "filter/invert", "invert", "invert({v})", FilterComposition, 958, ("", "100%"), ("0", "0"));
        Family("saturate", "filter/saturate", "saturate", "saturate({v})", FilterComposition, 959,
            ("0", "0"), ("50", ".5"), ("100", "1"), ("150", "1.5"), ("200", "2"));
        Family("sepia", "filter/sepia", "sepia", "sepia({v})", FilterComposition, 960, ("", "100%"), ("0", "0"));
        Family("drop-shadow", "filter/drop-shadow", "drop-shadow", "drop-shadow({v})", FilterComposition, 961,
            ("none", "0 0 #0000"),
            ("sm", "0 1px 1px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.05))"),
            ("", "0 1px 2px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.1))) drop-shadow(0 1px 1px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.06))"),
            ("md", "0 3px 3px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.12))"),
            ("lg", "0 4px 4px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.15))"),
            ("xl", "0 9px 7px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.1))"),
            ("2xl", "0 25px 25px var(--khamsin-drop-shadow-color,rgb(0 0 0 / 0.15))"));
        entries.Add(new UtilityEntry("drop-shadow", UtilityKind.Color, "filter/drop-shadow-color", 962,
            template: "--khamsin-drop-shadow-color:{v}"));

        Family("backdrop-blur", "backdrop/blur", "backdrop-blur", "blur({v})", BackdropComposition, 963,
            ("none", "0"), ("sm", "4px"), ("", "8px"), ("md", "12px"), ("lg", "16px"), ("xl", "24px"), ("2xl", "40px"), ("3xl", "64px"));
        Family("backdrop-brightness", "backdrop/brightness", "backdrop-brightness", "brightness({v})", BackdropComposition, 964, PercentScales());
        Family("backdrop-contrast", "backdrop/contrast", "backdrop-contrast", "contrast({v})", BackdropComposition, 965, PercentScales());
        Family("backdrop-grayscale", "backdrop/grayscale", "backdrop-grayscale", "grayscale({v})", BackdropComposition, 966, ("", "100%"), ("0", "0"));
        Family("backdrop-hue-rotate", "backdrop/hue-rotate", "backdrop-hue-rotate", "hue-rotate({v})", BackdropComposition, 967,
            ("0", "0deg"), ("15", "15deg"), ("30", "30deg"), ("60", "60deg"), ("90", "90deg"), ("180", "180deg"));
        Family("backdrop-invert", "backdrop/invert", "backdrop-invert", "invert({v})", BackdropComposition, 968, ("", "100%"), ("0", "0"));
        Family("backdrop-saturate", "backdrop/saturate", "backdrop-saturate", "saturate({v})", BackdropComposition, 969,
            ("0", "0"), ("50", ".5"), ("100", "1"), ("150", "1.5"), ("200", "2"));
        Family("backdrop-sepia", "backdrop/sepia", "backdrop-sepia", "sepia({v})", BackdropComposition, 970, ("", "100%"), ("0", "0"));

        return entries;

        (string, string)[] PercentScales() =>
        [
            ("0", "0"), ("50", ".5"), ("75", ".75"), ("90", ".9"), ("95", ".95"), ("100", "1"),
            ("105", "1.05"), ("110", "1.1"), ("125", "1.25"), ("150", "1.5"), ("200", "2"),
        ];
    }

    private static UtilityEntry TextSizeEntry(Theme theme)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var pair in theme.TextSizes) 
            values[pair.Key] = "font-size:var(--text-" + pair.Key + ");line-height:var(--text-" + pair.Key + "--line-height)";

        return new UtilityEntry("text", UtilityKind.Enum, "text-size", 600, template: "font-size:{v}", values: values);
    }

    private static UtilityEntry Static(string prefix, string declarations, string group, int order, string? childSelector = null) =>
        new(
            prefix,
            UtilityKind.Static,
            group,
            order,
            values: new Dictionary<string, string>(Ordinal) { [string.Empty] = declarations },
            childSelector: childSelector);

    private static UtilityEntry Spacing(
        string prefix,
        string property,
        string group,
        int order,
        bool negative = false,
        bool fraction = false,
        Dictionary<string, string>? keywords = null,
        string? childSelector = null)
    {
        var template = property + ":{v}";
        if (prefix == "size") template = "width:{v};height:{v}";

        return new UtilityEntry(
            prefix,
            UtilityKind.Spacing,
            group,
            order,
            template: template,
            keywords: keywords,
            allowNegative: negative,
            acceptsFraction: fraction,
            childSelector: childSelector);
    }

    private static UtilityEntry Color(string prefix, string property, string group, int order,
        string? childSelector = null)
        => new(prefix, UtilityKind.Color, group, order, template: property + ":{v}", childSelector: childSelector);

    private static UtilityEntry Enum(string prefix, string group, int order, string template,
        params (string Value, string Declarations)[] values)
        => new(
            prefix,
            UtilityKind.Enum,
            group,
            order,
            template: template,
            values: ToDictionary(values));

    private static UtilityEntry TokenEnum(
        string prefix,
        string group,
        int order,
        string property,
        string tokenPrefix,
        IEnumerable<KeyValuePair<string, string>> tokens,
        string template,
        bool arbitrary = true)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var token in tokens) 
            values[token.Key] = property + ":var(--" + tokenPrefix + "-" + token.Key + ")";

        return new UtilityEntry(prefix, UtilityKind.Enum, group, order, template: template, values: values, acceptsArbitrary: arbitrary);
    }

    private static UtilityEntry MultiTokenEnum(
        string prefix,
        string group,
        int order,
        string properties,
        string tokenPrefix,
        IEnumerable<KeyValuePair<string, string>> tokens)
    {
        var propertyList = properties.Split(',');
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var token in tokens)
        {
            var declaration = string.Empty;
            foreach (var property in propertyList)
            {
                if (declaration.Length > 0) declaration += ";";

                declaration += property + ":var(--" + tokenPrefix + "-" + token.Key + ")";
            }

            values[token.Key] = declaration;
        }

        return new UtilityEntry(prefix, UtilityKind.Enum, group, order, template: propertyList[0] + ":{v}", values: values);
    }

    private static Dictionary<string, string> ToDictionary(params (string Value, string Declarations)[] values)
    {
        var dictionary = new Dictionary<string, string>(Ordinal);
        foreach (var value in values) dictionary[value.Value] = value.Declarations;

        return dictionary;
    }

    private static Dictionary<string, string> Keywords(params (string Value, string Css)[] values) => ToDictionary(values);

    private static Dictionary<string, string> AutoOnly() => Keywords(("auto", "auto"));

    /// <summary>Inset offsets accept <c>auto</c> and the <c>full</c> keyword (100%).</summary>
    private static Dictionary<string, string> InsetKeywords() => Keywords(("auto", "auto"), ("full", "100%"));

    /// <summary>Per-side border width steps beyond the bare 1px form.</summary>
    private static (string Value, string Declarations)[] SideWidthValues(string property) =>
    [
        ("0", property + ":0;border-style:solid"),
        ("2", property + ":2px;border-style:solid"),
        ("4", property + ":4px;border-style:solid"),
        ("8", property + ":8px;border-style:solid"),
    ];

    private const string NumericComposition =
        "font-variant-numeric:var(--khamsin-numeric,) var(--khamsin-ordinal,) var(--khamsin-slashed-zero,) var(--khamsin-fraction,)";

    /// <summary>Font-variant-numeric slots compose so tabular figures can pair with ordinals.</summary>
    private static List<UtilityEntry> NumericEntries()
    {
        var entries = new List<UtilityEntry>(6);

        void Slot(string prefix, string group, string variable, int order, params (string Value, string Keyword)[] steps)
        {
            var values = new Dictionary<string, string>(Ordinal);
            foreach (var (value, keyword) in steps)
                values[value] = "--khamsin-" + variable + ":" + keyword + ";" + NumericComposition;

            entries.Add(new UtilityEntry(prefix, UtilityKind.Enum, group, order, values: values));
        }

        Slot("tabular-nums", "numeric/tabular", "numeric", 706, ("", "tabular-nums"));
        Slot("proportional-nums", "numeric/tabular", "numeric", 707, ("", "proportional-nums"));
        Slot("lining-nums", "numeric/lining", "numeric", 708, ("", "lining-nums"));
        Slot("oldstyle-nums", "numeric/lining", "numeric", 709, ("", "oldstyle-nums"));
        Slot("ordinal", "numeric/ordinal", "ordinal", 710, ("", "ordinal"));
        Slot("slashed-zero", "numeric/slashed-zero", "slashed-zero", 711, ("", "slashed-zero"));
        Slot("diagonal-fractions", "numeric/fraction", "fraction", 712, ("", "diagonal-fractions"));
        Slot("stacked-fractions", "numeric/fraction", "fraction", 713, ("", "stacked-fractions"));
        entries.Add(new UtilityEntry("normal-nums", UtilityKind.Enum, "numeric/normal", 714, values: ToDictionary(
            ("", "--khamsin-numeric:normal;--khamsin-ordinal:normal;--khamsin-slashed-zero:normal;--khamsin-fraction:normal;" + NumericComposition))));

        return entries;
    }

    private static Dictionary<string, string> WidthKeywords() => Keywords(
        ("auto", "auto"),
        ("full", "100%"),
        ("screen", "100vw"),
        ("min", "min-content"),
        ("max", "max-content"),
        ("fit", "fit-content"));

    private static Dictionary<string, string> HeightKeywords() => Keywords(
        ("auto", "auto"),
        ("full", "100%"),
        ("screen", "100vh"),
        ("dvh", "100dvh"),
        ("svh", "100svh"),
        ("lvh", "100lvh"),
        ("min", "min-content"),
        ("max", "max-content"),
        ("fit", "fit-content"));

    private static Dictionary<string, string> MinKeywords() => Keywords(
        ("0", "0"),
        ("full", "100%"),
        ("min", "min-content"),
        ("max", "max-content"),
        ("fit", "fit-content"));

    private static Dictionary<string, string> MaxWidthKeywords(Theme theme)
    {
        var keywords = Keywords(
            ("none", "none"),
            ("full", "100%"),
            ("screen", "100vw"),
            ("min", "min-content"),
            ("max", "max-content"),
            ("fit", "fit-content"));

        foreach (var container in theme.Containers) 
            keywords[container.Key] = "var(--container-" + container.Key + ")";

        return keywords;
    }

    private static (string Value, string Declarations)[] BuildOrderValues()
    {
        var values = new List<(string, string)>();
        for (var i = 1; i <= 12; i++) values.Add((i.ToString(), "order:" + i));

        values.Add(("first", "order:-9999"));
        values.Add(("last", "order:9999"));
        values.Add(("none", "order:0"));
        return values.ToArray();
    }

    private static (string Value, string Declarations)[] BuildRepeatValues(string property, int count)
    {
        var values = new List<(string, string)>();
        for (var i = 1; i <= count; i++) values.Add((i.ToString(), property + ":repeat(" + i + ",minmax(0,1fr))"));

        return values.ToArray();
    }

    private static (string Value, string Declarations)[] BuildSpanValues(string property, int count, string extraKey, string extraDeclarations)
    {
        var values = new List<(string, string)>();
        for (var i = 1; i <= count; i++) values.Add((i.ToString(), property + ":span " + i + "/span " + i));

        values.Add((extraKey, extraDeclarations));
        return values.ToArray();
    }

    private static (string Value, string Declarations)[] BuildRangeValues(string property, int start, int end, string extraKey, string extraDeclarations)
    {
        var values = new List<(string, string)>();
        for (var i = start; i < end; i++) values.Add((i.ToString(), property + ":" + i));

        values.Add((extraKey, extraDeclarations));
        return values.ToArray();
    }

    /// <summary>
    /// v4 inset rings: the ring composition with the inset variable pre-set, sharing the ring
    /// color so <c>inset-ring-{color}</c> is simply the ring color.
    /// </summary>
    private static List<UtilityEntry> InsetRingEntry()
    {
        const string shadow = "box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-width) var(--khamsin-ring-color,currentColor)";
        var values = new Dictionary<string, string>(Ordinal)
        {
            ["0"] = "--khamsin-ring-inset:inset;--khamsin-ring-width:0;box-shadow:var(--khamsin-ring-inset,) 0 0 0 0 var(--khamsin-ring-color,currentColor)",
            ["1"] = "--khamsin-ring-inset:inset;--khamsin-ring-width:1px;" + shadow,
            ["2"] = "--khamsin-ring-inset:inset;--khamsin-ring-width:2px;" + shadow,
            ["4"] = "--khamsin-ring-inset:inset;--khamsin-ring-width:4px;" + shadow,
            ["8"] = "--khamsin-ring-inset:inset;--khamsin-ring-width:8px;" + shadow,
        };

        const string arbitrary = "--khamsin-ring-inset:inset;--khamsin-ring-width:{v};" + shadow;
        return new List<UtilityEntry>
        {
            new(
                "inset-ring",
                UtilityKind.Enum,
                "inset-ring",
                928,
                template: arbitrary,
                values: values,
                arbitraryTemplate: arbitrary),
            new("inset-ring", UtilityKind.Color, "ring-color", 929, template: "--khamsin-ring-color:{v}"),
        };
    }

    /// <summary>v4 inset shadows carry their color through a variable so <c>inset-shadow-{color}</c> recolors them.</summary>
    private static List<UtilityEntry> InsetShadowEntries()
    {
        var values = new Dictionary<string, string>(Ordinal)
        {
            ["2xs"] = "--khamsin-inset-shadow:inset 0 1px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.05));box-shadow:var(--khamsin-inset-shadow)",
            ["xs"] = "--khamsin-inset-shadow:inset 0 1px 1px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.05));box-shadow:var(--khamsin-inset-shadow)",
            ["sm"] = "--khamsin-inset-shadow:inset 0 2px 4px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.05));box-shadow:var(--khamsin-inset-shadow)",
            ["md"] = "--khamsin-inset-shadow:inset 0 4px 8px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.06));box-shadow:var(--khamsin-inset-shadow)",
            ["lg"] = "--khamsin-inset-shadow:inset 0 8px 12px var(--khamsin-inset-shadow-color,rgb(0 0 0 / 0.08));box-shadow:var(--khamsin-inset-shadow)",
            ["none"] = "--khamsin-inset-shadow:0 0 #0000;box-shadow:var(--khamsin-inset-shadow)",
        };

        return new List<UtilityEntry>
        {
            new(
                "inset-shadow",
                UtilityKind.Enum,
                "inset-shadow",
                902,
                template: "--khamsin-inset-shadow:{v};box-shadow:var(--khamsin-inset-shadow)",
                values: values,
                arbitraryTemplate: "--khamsin-inset-shadow:{v};box-shadow:var(--khamsin-inset-shadow)"),
            new("inset-shadow", UtilityKind.Color, "inset-shadow-color", 903, template: "--khamsin-inset-shadow-color:{v}"),
        };
    }

    /// <summary>v4 text shadows follow the same color-variable pattern as inset shadows.</summary>
    private static List<UtilityEntry> TextShadowEntries()
    {
        var values = new Dictionary<string, string>(Ordinal)
        {
            ["2xs"] = "text-shadow:0 1px 0 var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.15))",
            ["xs"] = "text-shadow:0 1px 1px var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.2))",
            ["sm"] = "text-shadow:0 1px 2px var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.25))",
            ["md"] = "text-shadow:0 2px 4px var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.25))",
            ["lg"] = "text-shadow:0 4px 8px var(--khamsin-text-shadow-color,rgb(0 0 0 / 0.3))",
            ["none"] = "text-shadow:none",
        };

        return new List<UtilityEntry>
        {
            new(
                "text-shadow",
                UtilityKind.Enum,
                "text-shadow",
                715,
                template: "text-shadow:{v}",
                values: values,
                arbitraryTemplate: "text-shadow:{v}"),
            new("text-shadow", UtilityKind.Color, "text-shadow-color", 716, template: "--khamsin-text-shadow-color:{v}"),
        };
    }

    /// <summary>
    /// The v4 mask family. Edge, radial, linear and conic layers each live in their own
    /// --khamsin-mask-* variable and compose through the shared mask-image declaration; from/to
    /// stops are positions in 5% steps (colors via the mask-[…] arbitrary form).
    /// </summary>
    private static List<UtilityEntry> MaskEntries()
    {
        var entries = new List<UtilityEntry>(70);
        const string composition = "mask-image:var(--khamsin-mask-top,) var(--khamsin-mask-right,) var(--khamsin-mask-bottom,) var(--khamsin-mask-left,) var(--khamsin-mask-radial,) var(--khamsin-mask-linear,) var(--khamsin-mask-conic,);mask-composite:intersect;";

        static string EdgeGradient(string edge)
        {
            var direction = edge == "top" ? "to bottom"
                : edge == "bottom" ? "to top"
                : edge == "left" ? "to right"
                : "to left";
            return "linear-gradient(" + direction + ",transparent var(--khamsin-mask-" + edge + "-from,0%),black var(--khamsin-mask-" + edge + "-to,100%))";
        }

        void AddEdge(string alias, string edge, int order)
        {
            var gradient = "--khamsin-mask-" + edge + ":" + EdgeGradient(edge) + ";" + composition;
            var from = new Dictionary<string, string>(Ordinal);
            var to = new Dictionary<string, string>(Ordinal);
            for (var percent = 0; percent <= 100; percent += 5)
            {
                var token = percent + "%";
                from[token] = "--khamsin-mask-" + edge + "-from:" + token + ";" + gradient;
                to[token] = "--khamsin-mask-" + edge + "-to:" + token + ";" + gradient;
            }

            entries.Add(new UtilityEntry(alias + "-from", UtilityKind.Enum, "mask/" + edge + "-from", order, values: from));
            entries.Add(new UtilityEntry(alias + "-to", UtilityKind.Enum, "mask/" + edge + "-to", order, values: to));
        }

        void AddEdgePair(string alias, string edgeA, string edgeB, int order)
        {
            var gradient = "--khamsin-mask-" + edgeA + ":" + EdgeGradient(edgeA) + ";--khamsin-mask-" + edgeB + ":" + EdgeGradient(edgeB) + ";" + composition;
            var from = new Dictionary<string, string>(Ordinal);
            var to = new Dictionary<string, string>(Ordinal);
            for (var percent = 0; percent <= 100; percent += 5)
            {
                var token = percent + "%";
                from[token] = "--khamsin-mask-" + edgeA + "-from:" + token + ";--khamsin-mask-" + edgeB + "-from:" + token + ";" + gradient;
                to[token] = "--khamsin-mask-" + edgeA + "-to:" + token + ";--khamsin-mask-" + edgeB + "-to:" + token + ";" + gradient;
            }

            entries.Add(new UtilityEntry(alias + "-from", UtilityKind.Enum, "mask/" + alias + "-from", order, values: from));
            entries.Add(new UtilityEntry(alias + "-to", UtilityKind.Enum, "mask/" + alias + "-to", order, values: to));
        }

        UtilityEntry PercentUtility(string prefix, string group, string variable, string trailing, int order)
        {
            var values = new Dictionary<string, string>(Ordinal);
            for (var percent = 0; percent <= 100; percent += 5)
            {
                var token = percent + "%";
                values[token] = variable + ":" + token + ";" + trailing;
            }

            return new UtilityEntry(prefix, UtilityKind.Enum, group, order, values: values);
        }

        AddEdge("mask-t", "top", 975);
        AddEdge("mask-r", "right", 975);
        AddEdge("mask-b", "bottom", 975);
        AddEdge("mask-l", "left", 975);
        AddEdgePair("mask-x", "left", "right", 975);
        AddEdgePair("mask-y", "top", "bottom", 975);

        const string radialGradient = "--khamsin-mask-radial:radial-gradient(var(--khamsin-mask-radial-shape,circle) var(--khamsin-mask-radial-size,closest-side) at var(--khamsin-mask-radial-position,center),transparent var(--khamsin-mask-radial-from,0%),black var(--khamsin-mask-radial-to,100%));" + composition;
        entries.Add(PercentUtility("mask-radial-from", "mask/radial-from", "--khamsin-mask-radial-from", radialGradient, 981));
        entries.Add(PercentUtility("mask-radial-to", "mask/radial-to", "--khamsin-mask-radial-to", radialGradient, 981));
        entries.Add(new UtilityEntry("mask-circle", UtilityKind.Enum, "mask/radial-shape", 981, values: ToDictionary(("", "--khamsin-mask-radial-shape:circle;" + radialGradient))));
        entries.Add(new UtilityEntry("mask-ellipse", UtilityKind.Enum, "mask/radial-shape", 981, values: ToDictionary(("", "--khamsin-mask-radial-shape:ellipse;" + radialGradient))));
        entries.Add(new UtilityEntry("mask-closest-side", UtilityKind.Enum, "mask/radial-size", 981, values: ToDictionary(("", "--khamsin-mask-radial-size:closest-side;" + radialGradient))));
        entries.Add(new UtilityEntry("mask-closest-corner", UtilityKind.Enum, "mask/radial-size", 981, values: ToDictionary(("", "--khamsin-mask-radial-size:closest-corner;" + radialGradient))));
        entries.Add(new UtilityEntry("mask-farthest-side", UtilityKind.Enum, "mask/radial-size", 981, values: ToDictionary(("", "--khamsin-mask-radial-size:farthest-side;" + radialGradient))));
        entries.Add(new UtilityEntry("mask-farthest-corner", UtilityKind.Enum, "mask/radial-size", 981, values: ToDictionary(("", "--khamsin-mask-radial-size:farthest-corner;" + radialGradient))));
        foreach (var position in new[] { "center", "top", "bottom", "left", "right" })
            entries.Add(new UtilityEntry("mask-radial-at-" + position, UtilityKind.Enum, "mask/radial-position", 981, values: ToDictionary(("", "--khamsin-mask-radial-position:" + position + ";" + radialGradient))));

        const string linearGradient = "--khamsin-mask-linear:linear-gradient(var(--khamsin-mask-linear-angle,0deg),transparent var(--khamsin-mask-linear-from,0%),black var(--khamsin-mask-linear-to,100%));" + composition;
        entries.Add(PercentUtility("mask-linear-from", "mask/linear-from", "--khamsin-mask-linear-from", linearGradient, 982));
        entries.Add(PercentUtility("mask-linear-to", "mask/linear-to", "--khamsin-mask-linear-to", linearGradient, 982));
        var angleValues = new Dictionary<string, string>(Ordinal);
        for (var angle = 0; angle <= 360; angle += 15)
        {
            var token = angle.ToString();
            angleValues[token] = "--khamsin-mask-linear-angle:" + token + "deg;" + linearGradient;
        }

        entries.Add(new UtilityEntry("mask-linear", UtilityKind.Enum, "mask/linear-angle", 982, values: angleValues, arbitraryTemplate: "--khamsin-mask-linear-angle:{v};" + linearGradient));

        const string conicGradient = "--khamsin-mask-conic:conic-gradient(var(--khamsin-mask-conic-angle,0deg),transparent var(--khamsin-mask-conic-from,0%),black var(--khamsin-mask-conic-to,100%));" + composition;
        entries.Add(PercentUtility("mask-conic-from", "mask/conic-from", "--khamsin-mask-conic-from", conicGradient, 983));
        entries.Add(PercentUtility("mask-conic-to", "mask/conic-to", "--khamsin-mask-conic-to", conicGradient, 983));
        var conicAngleValues = new Dictionary<string, string>(Ordinal);
        for (var angle = 0; angle <= 360; angle += 15)
        {
            var token = angle.ToString();
            conicAngleValues[token] = "--khamsin-mask-conic-angle:" + token + "deg;" + conicGradient;
        }

        entries.Add(new UtilityEntry("mask-conic", UtilityKind.Enum, "mask/conic-angle", 983, values: conicAngleValues, arbitraryTemplate: "--khamsin-mask-conic-angle:{v};" + conicGradient));

        entries.Add(new UtilityEntry("mask-none", UtilityKind.Static, "mask", 989, values: ToDictionary(("", "mask-image:none"))));
        entries.Add(new UtilityEntry("mask", UtilityKind.Enum, "mask", 989, arbitraryTemplate: "mask-image:{v}"));
        entries.Add(new UtilityEntry("mask-clip", UtilityKind.Enum, "mask/clip", 990, template: "mask-clip:{v}", values: ToDictionary(
            ("border", "mask-clip:border-box"), ("padding", "mask-clip:padding-box"), ("content", "mask-clip:content-box"), ("fill", "mask-clip:fill-box"), ("stroke", "mask-clip:stroke-box"), ("view", "mask-clip:view-box"))));
        entries.Add(new UtilityEntry("mask-composite", UtilityKind.Enum, "mask/composite", 991, template: "mask-composite:{v}", values: ToDictionary(
            ("add", "mask-composite:add"), ("subtract", "mask-composite:subtract"), ("intersect", "mask-composite:intersect"), ("exclude", "mask-composite:exclude"))));
        entries.Add(new UtilityEntry("mask-mode", UtilityKind.Enum, "mask/mode", 992, template: "mask-mode:{v}", values: ToDictionary(
            ("alpha", "mask-mode:alpha"), ("luminance", "mask-mode:luminance"), ("match-source", "mask-mode:match-source"))));

        return entries;
    }

    private static UtilityEntry RingWidthEntry()
    {
        const string declarations = "--khamsin-ring-width:{v};box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-offset-width,0px) var(--khamsin-ring-offset-color,#fff), var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-width) var(--khamsin-ring-color,currentColor)";
        return new UtilityEntry(
            "ring",
            UtilityKind.Enum,
            "ring-width",
            921,
            template: declarations,
            values: ToDictionary(
                ("0", "--khamsin-ring-width:0;" + RingShadow("0")),
                ("1", "--khamsin-ring-width:1px;" + RingShadow("1px")),
                ("2", "--khamsin-ring-width:2px;" + RingShadow("2px")),
                ("4", "--khamsin-ring-width:4px;" + RingShadow("4px")),
                ("8", "--khamsin-ring-width:8px;" + RingShadow("8px"))),
            arbitraryTemplate: declarations);
    }

    /// <summary>The ring box-shadow with the width baked in; the offset segment always precedes it.</summary>
    private static string RingShadow(string width) =>
        "box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-offset-width,0px) var(--khamsin-ring-offset-color,#fff), var(--khamsin-ring-inset,) 0 0 0 " + width + " var(--khamsin-ring-color,currentColor)";

    /// <summary>v3 ring offsets: an extra solid shadow ring between the element and the colored ring.</summary>
    private static List<UtilityEntry> RingOffsetEntries()
    {
        var widthDeclarations = "--khamsin-ring-offset-width:{v};" + RingShadowVar();
        return new List<UtilityEntry>
        {
            new(
                "ring-offset",
                UtilityKind.Enum,
                "ring-offset",
                922,
                values: ToDictionary(
                    ("0", "--khamsin-ring-offset-width:0px;" + RingShadowVar()),
                    ("1", "--khamsin-ring-offset-width:1px;" + RingShadowVar()),
                    ("2", "--khamsin-ring-offset-width:2px;" + RingShadowVar()),
                    ("4", "--khamsin-ring-offset-width:4px;" + RingShadowVar()),
                    ("8", "--khamsin-ring-offset-width:8px;" + RingShadowVar())),
                arbitraryTemplate: widthDeclarations),
            new("ring-offset", UtilityKind.Color, "ring-offset-color", 923,
                template: "--khamsin-ring-offset-color:{v};" + RingShadowVar()),
        };
    }

    private static string RingShadowVar() =>
        "box-shadow:var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-offset-width,0px) var(--khamsin-ring-offset-color,#fff), var(--khamsin-ring-inset,) 0 0 0 var(--khamsin-ring-width) var(--khamsin-ring-color,currentColor)";

    /// <summary>
    /// v4 inset rings: the ring composition with the inset variable pre-set, sharing the ring
    /// color so <c>inset-ring-{color}</c> is simply the ring color.
    /// </summary>
    private static UtilityEntry OutlineWidthEntry()
    {
        return new UtilityEntry(
            "outline",
            UtilityKind.Enum,
            "outline-width",
            931,
            template: "outline-style:solid;outline-width:{v}",
            values: ToDictionary(
                ("0", "outline-width:0"),
                ("1", "outline-style:solid;outline-width:1px"),
                ("2", "outline-style:solid;outline-width:2px"),
                ("4", "outline-style:solid;outline-width:4px"),
                ("8", "outline-style:solid;outline-width:8px")),
            arbitraryTemplate: "outline-style:solid;outline-width:{v}");
    }

    private static (string Value, string Declarations)[] BuildFontWeights() =>
    [
        ("thin", "font-weight:100"),
        ("extralight", "font-weight:200"),
        ("light", "font-weight:300"),
        ("normal", "font-weight:400"),
        ("medium", "font-weight:500"),
        ("semibold", "font-weight:600"),
        ("bold", "font-weight:700"),
        ("extrabold", "font-weight:800"),
        ("black", "font-weight:900")
    ];

    private static (string Value, string Declarations)[] BuildOpacityValues()
    {
        var values = new List<(string, string)>();
        for (var i = 0; i <= 100; i += 5) values.Add((i.ToString(), "opacity:" + SpacingScale.FormatNumber(i / 100d)));

        return values.ToArray();
    }

    private static UtilityEntry FontFamilyEntry(Theme theme)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var family in theme.Fonts)
            values[family.Key] = "font-family:var(--font-" + family.Key + ")";

        return new UtilityEntry(
            "font",
            UtilityKind.Enum,
            "font-family",
            610,
            template: "font-family:{v}",
            values: values);
    }

    private static Dictionary<string, string> SizeKeywords() => Keywords(
        ("auto", "auto"),
        ("full", "100%"),
        ("min", "min-content"),
        ("max", "max-content"),
        ("fit", "fit-content"));

    private static readonly (string Key, string Value)[] ScaleSteps =
    [
        ("0", "0"),
        ("50", ".5"),
        ("75", ".75"),
        ("90", ".9"),
        ("95", ".95"),
        ("100", "1"),
        ("105", "1.05"),
        ("110", "1.1"),
        ("125", "1.25"),
        ("150", "1.5"),
        ("200", "2")
    ];

    private static UtilityEntry ScaleEntry()
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var step in ScaleSteps) 
            values[step.Key] = "--khamsin-scale-x:" + step.Value + ";--khamsin-scale-y:" + step.Value + ";scale:var(--khamsin-scale-x) var(--khamsin-scale-y)";

        return new UtilityEntry(
            "scale",
            UtilityKind.Enum,
            "scale",
            564,
            template: "scale:{v}",
            values: values,
            arbitraryTemplate: "--khamsin-scale-x:{v};--khamsin-scale-y:{v};scale:var(--khamsin-scale-x) var(--khamsin-scale-y)");
    }

    private static UtilityEntry ScaleAxisEntry(string prefix, string group, int order, bool xAxis)
    {
        var values = new Dictionary<string, string>(Ordinal);
        foreach (var step in ScaleSteps)
        {
            values[step.Key] = xAxis
                ? "--khamsin-scale-x:" + step.Value + ";scale:var(--khamsin-scale-x) var(--khamsin-scale-y,1)"
                : "--khamsin-scale-y:" + step.Value + ";scale:var(--khamsin-scale-x,1) var(--khamsin-scale-y)";
        }

        return new UtilityEntry(
            prefix,
            UtilityKind.Enum,
            group,
            order,
            template: "scale:{v}",
            values: values,
            arbitraryTemplate: xAxis
                ? "--khamsin-scale-x:{v};scale:var(--khamsin-scale-x) var(--khamsin-scale-y,1)"
                : "--khamsin-scale-y:{v};scale:var(--khamsin-scale-x,1) var(--khamsin-scale-y)");
    }

    private static Dictionary<string, string> LineClampValues()
    {
        var values = new Dictionary<string, string>(Ordinal)
        {
            ["none"] = "display:block;overflow:visible;-webkit-line-clamp:unset",
        };

        for (var i = 1; i <= 6; i++) 
            values[i.ToString()] = "display:-webkit-box;overflow:hidden;-webkit-box-orient:vertical;-webkit-line-clamp:" + i;

        return values;
    }

    private static Dictionary<string, string> ColumnValues()
    {
        var values = new Dictionary<string, string>(Ordinal)
        {
            ["auto"] = "columns:auto",
        };

        for (var i = 1; i <= 12; i++) values[i.ToString()] = "columns:" + i;

        return values;
    }
}
