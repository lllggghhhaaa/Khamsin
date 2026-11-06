using System.Diagnostics;
using System.Text;

namespace Khamsin.Build.Tests;

/// <summary>
/// Writes a throw-away Blazor app and class library that reference this repository's projects, then
/// builds them with the real SDK so the MSBuild wiring is exercised, not simulated.
/// </summary>
internal sealed class BuildFixture : IDisposable
{
    private static readonly object BuildLock = new();
    private string? _css;
    private bool _minify;

    public BuildFixture(string? homeMarkup = null, bool minify = true)
    {
        _minify = minify;
        Root = Path.Combine(Path.GetTempPath(), "khamsin-build-tests", Guid.NewGuid().ToString("N"));
        RepositoryRoot = FindRepositoryRoot();
        Configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        Directory.CreateDirectory(Path.Combine(Root, "Lib", "Components"));
        Directory.CreateDirectory(Path.Combine(Root, "App", "Components"));

        WriteProjects();
        WriteLibrary();
        WriteApp(homeMarkup);
    }

    public string Root { get; }

    public string RepositoryRoot { get; }

    public string Configuration { get; }

    public string AppProject => Path.Combine(Root, "App", "App.csproj");

    public string CssPath => Path.Combine(Root, "App", "obj", Configuration, "net8.0", "khamsin.css");

    public string Css => _css ??= File.ReadAllText(CssPath);

    /// <summary>Rewrites the generated project with a different <c>KhamsinMinify</c> value.</summary>
    public void SetMinify(bool minify)
    {
        _minify = minify;
        WriteProjects();
    }

    /// <summary>Writes or removes the app's <c>khamsin.json</c>; null removes the file.</summary>
    public void SetConfig(string? json)
    {
        var path = Path.Combine(Root, "App", "khamsin.json");
        if (json is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        File.WriteAllText(path, json);
    }

    /// <summary>Writes or removes the library's <c>khamsin.json</c>; null removes the file.</summary>
    public void SetLibraryConfig(string? json)
    {
        var path = Path.Combine(Root, "Lib", "khamsin.json");
        if (json is null)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        File.WriteAllText(path, json);
    }

    public BuildResult Build(string verbosity = "minimal")
    {
        lock (BuildLock)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(AppProject);
            startInfo.ArgumentList.Add("-nologo");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(Configuration);
            startInfo.ArgumentList.Add("-v:" + verbosity);
            startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet could not be started.");
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null) output.AppendLine(args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null) error.AppendLine(args.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(240_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("dotnet build did not finish within four minutes.");
            }

            process.WaitForExit();
            _css = null;
            return new BuildResult(process.ExitCode, output.ToString(), error.ToString());
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WriteProjects()
    {
        var coreAnalyzer = Path.Combine(RepositoryRoot, "Khamsin.Core", "bin", Configuration, "netstandard2.0", "Khamsin.Core.dll");
        var blazor = Path.Combine(RepositoryRoot, "Khamsin.Blazor", "Khamsin.Blazor.csproj");
        var core = Path.Combine(RepositoryRoot, "Khamsin.Core", "Khamsin.Core.csproj");
        var generators = Path.Combine(RepositoryRoot, "Khamsin.Generators", "Khamsin.Generators.csproj");
        var build = Path.Combine(RepositoryRoot, "Khamsin.Build", "Khamsin.Build.csproj");
        var task = Path.Combine(RepositoryRoot, "Khamsin.Build", "bin", Configuration, "net8.0", "Khamsin.Build.dll");
        var props = Path.Combine(RepositoryRoot, "Khamsin.Build", "buildTransitive", "Khamsin.props");
        var targets = Path.Combine(RepositoryRoot, "Khamsin.Build", "buildTransitive", "Khamsin.targets");

        File.WriteAllText(Path.Combine(Root, "Lib", "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <RootNamespace>SampleLib</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <KhamsinEmitCss>false</KhamsinEmitCss>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
              <ItemGroup>
                <Analyzer Include="{coreAnalyzer}" />
                <ProjectReference Include="{blazor}" />
                <ProjectReference Include="{core}" />
                <ProjectReference Include="{generators}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <Import Project="{props}" />
            </Project>
            """);

        File.WriteAllText(AppProject, $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <RootNamespace>SampleApp</RootNamespace>
                <ImplicitUsings>enable</ImplicitUsings>
                <KhamsinEmitCss>true</KhamsinEmitCss>
                <KhamsinMinify>{(_minify ? "true" : "false")}</KhamsinMinify>
                <KhamsinTaskAssembly>{task}</KhamsinTaskAssembly>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
              <ItemGroup>
                <Analyzer Include="{coreAnalyzer}" />
                <ProjectReference Include="{blazor}" />
                <ProjectReference Include="{core}" />
                <ProjectReference Include="{generators}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Include="{build}" ReferenceOutputAssembly="false" />
                <ProjectReference Include="..\Lib\Lib.csproj" />
              </ItemGroup>
              <Import Project="{props}" />
              <Import Project="{targets}" />
            </Project>
            """);
    }

    private void WriteLibrary()
    {
        var components = Path.Combine(Root, "Lib", "Components");
        File.WriteAllText(Path.Combine(Root, "Lib", "_Imports.razor"), """
            @using Microsoft.AspNetCore.Components
            @using Microsoft.AspNetCore.Components.Web
            @using Khamsin
            """);

        File.WriteAllText(Path.Combine(components, "Card.razor"), """
            @inherits KhamsinComponentBase
            <section class="@(Defaults + Khamsin)">
                <h2 class="text-lg font-semibold">@Title</h2>
                <p class="mt-2">@ChildContent</p>
            </section>

            @code {
                private static readonly KhamsinClass Defaults = "rounded-lg border border-ink-200 bg-white p-4";

                [Parameter]
                public string Title { get; set; } = "Card";

                [Parameter]
                public RenderFragment? ChildContent { get; set; }
            }
            """);

        File.WriteAllText(Path.Combine(components, "Unused.razor"), """
            @inherits KhamsinComponentBase
            <section class="tracking-widest bg-rose-700">Never rendered by the app.</section>
            """);

        File.WriteAllText(Path.Combine(components, "Picker.razor"), """
            @typeparam TValue where TValue : class
            @inherits KhamsinComponentBase
            <section class="@(Defaults + Khamsin)">
                <span class="text-forest-600">Picker</span>
            </section>

            @code {
                private static readonly KhamsinClass Defaults = "bg-violet-700 text-white";

                [Parameter]
                public TValue Value { get; set; } = default!;
            }
            """);

        // Simulates an older package whose generic component never got a manifest of its own:
        // the generated manifest sits on a non-generic stub, and the child is only discoverable
        // through the generic type's IL.
        File.WriteAllText(Path.Combine(Root, "Lib", "Legacy.cs"), """
            using Khamsin;
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            namespace SampleLib.Legacy
            {
                [KhamsinManifest(Classes = "bg-amber-600")]
                public sealed class LegacyCard
                {
                }

                public sealed class LegacyCard<TValue> : ComponentBase
                {
                    [Parameter]
                    public TValue Value { get; set; } = default!;

                    protected override void BuildRenderTree(RenderTreeBuilder builder)
                    {
                        builder.OpenComponent<LegacyBadge>(0);
                        builder.CloseComponent();
                    }
                }

                [KhamsinManifest(Classes = "text-forest-600")]
                public sealed class LegacyBadge : ComponentBase
                {
                    protected override void BuildRenderTree(RenderTreeBuilder builder)
                    {
                        builder.AddContent(0, "badge");
                    }
                }
            }
            """);
    }

    private void WriteApp(string? homeMarkup)
    {
        var components = Path.Combine(Root, "App", "Components");
        File.WriteAllText(Path.Combine(Root, "App", "_Imports.razor"), """
            @using Microsoft.AspNetCore.Components
            @using Microsoft.AspNetCore.Components.Web
            @using Microsoft.AspNetCore.Components.Routing
            @using Khamsin
            @using SampleLib.Components
            """);

        File.WriteAllText(Path.Combine(components, "App.razor"), """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <link rel="stylesheet" href="khamsin.css" />
            </head>
            <body>
                <Routes />
            </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(components, "Routes.razor"), """
            <Router AppAssembly="typeof(Program).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="routeData" />
                </Found>
                <NotFound>
                    <p>Not found</p>
                </NotFound>
            </Router>
            """);

        File.WriteAllText(Path.Combine(components, "Home.razor"), homeMarkup ?? """
            @page "/"

            <h1 class="text-3xl tracking-wide">Home</h1>
            <Card Khamsin="@("p-8")">Body</Card>
            """);

        File.WriteAllText(Path.Combine(Root, "App", "Program.cs"), """
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            var app = builder.Build();
            app.UseStaticFiles();
            app.MapRazorComponents<SampleApp.Components.App>();
            app.Run();
            """);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Khamsin.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root (Khamsin.slnx).");
    }
}

internal sealed record BuildResult(int ExitCode, string Output, string Error)
{
    public string AllOutput => Output + Environment.NewLine + Error;
}
