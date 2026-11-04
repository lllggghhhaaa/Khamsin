using Khamsin;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// Demonstrates the endpoint fallback alongside the static web asset: same generated stylesheet,
// served with a content-hash ETag and a one-year immutable cache policy at a custom route.
app.MapKhamsinCss("/khamsin-endpoint.css");

app.MapRazorComponents<Sample.App.Components.App>().AddInteractiveServerRenderMode();

app.Run();
