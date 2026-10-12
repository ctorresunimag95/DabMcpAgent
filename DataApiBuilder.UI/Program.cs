using DataApiBuilder.UI.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("ai-agent", client =>
{
    client.BaseAddress = new Uri("https+http://ai-agent");
    client.Timeout = TimeSpan.FromMinutes(10);
}).AddServiceDiscovery();

builder.Services.AddSingleton<DataApiBuilder.UI.SchemaCache>();

var dabEndpoint = builder.Configuration["Dab__Endpoint"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("dab", client =>
{
    client.BaseAddress = new Uri(dabEndpoint.TrimEnd('/') + "/");
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();
