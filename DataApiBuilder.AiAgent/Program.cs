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
app.MapPost("/api/query", async (
    InvoiceRequest request,
    InvoiceQueryService invoiceQueryService,
    HttpContext httpContext,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "The 'text' property is required." });
    }

    try
    {
        var response = await invoiceQueryService.QueryAsync(request.Text, httpContext.RequestAborted);
        return Results.Ok(response);
    }
    catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
    {
        logger.LogInformation("Invoice query was canceled because the HTTP request was aborted.");
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (OperationCanceledException ex)
    {
        logger.LogWarning(ex, "Invoice query was canceled while waiting for the AI service.");
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
}).RequireRateLimiting("ai-query");

app.MapPost("/api/analyze", async (
    InvoiceRequest request,
    InvoiceAnalysisService invoiceAnalysisService,
    HttpContext httpContext,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return Results.BadRequest(new { error = "The 'text' property is required." });
    }

    try
    {
        var response = await invoiceAnalysisService.QueryAsync(request.Text, httpContext.RequestAborted);
        return Results.Ok(response);
    }
    catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
    {
        logger.LogInformation("Invoice analysis was canceled because the HTTP request was aborted.");
        return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
    }
    catch (OperationCanceledException ex)
    {
        logger.LogWarning(ex, "Invoice analysis was canceled while waiting for the AI service.");
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
}).RequireRateLimiting("ai-query");

app.Run();

public record InvoiceRequest(string Text);
