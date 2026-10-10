using DataApiBuilder.AiAgent.Extensions;
using DataApiBuilder.AiAgent.Services;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddInvoiceAgent(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ai-query", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

await app.InitializeDabMcpToolsAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseRateLimiter();
app.MapPost("/api/query", async (InvoiceRequest request, InvoiceQueryService invoiceQueryService, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "The 'text' property is required." });
    }

    var response = await invoiceQueryService.QueryAsync(request.Text, httpContext.RequestAborted);
    return Results.Ok(response);
}).RequireRateLimiting("ai-query");

app.MapPost("/api/analyze", async (InvoiceRequest request, InvoiceAnalysisService invoiceAnalysisService, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "The 'text' property is required." });
    }

    var response = await invoiceAnalysisService.QueryAsync(request.Text, httpContext.RequestAborted);
    return Results.Ok(response);
}).RequireRateLimiting("ai-query");

app.Run();

public record InvoiceRequest(string Text);
