using DataApiBuilder.AiAgent.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataApiBuilder.AiAgent.Services;

public sealed class InvoiceAnalysisService
{
    private readonly AIAgent _agent;
    private readonly DabMcpToolSessionProvider _mcpTools;

    public InvoiceAnalysisService(IServiceProvider services, DabMcpToolSessionProvider mcpTools)
    {
        _agent = services.GetRequiredKeyedService<AIAgent>(InvoiceAgentServiceCollectionExtensions.AnalysisAgentName);
        _mcpTools = mcpTools;
    }

    public async Task<InvoiceAnalysisResult> QueryAsync(string text, CancellationToken cancellationToken)
    {
        var mcpSession = _mcpTools.Session;
        var response = await _agent.RunAsync(
            text,
            options: new ChatClientAgentRunOptions(
                new ChatOptions
                {
                    Tools = mcpSession.Tools.ToList(),
                    MaxOutputTokens = 1500
                }),
            cancellationToken: cancellationToken);

        var usedTools = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(functionCall => functionCall.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var payload = ParsePayload(response.Text);
        var usage = response.Usage;
        return new InvoiceAnalysisResult(
            "1.0",
            usedTools,
            new ChartSpec(payload.ChartType, payload.Title, payload.Labels, payload.Datasets),
            payload.Summary,
            new InvoiceTokenUsage(
                usage?.InputTokenCount,
                usage?.OutputTokenCount,
                usage?.TotalTokenCount));
    }

    private static AnalysisAgentPayload ParsePayload(string responseText)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<AnalysisAgentPayload>(responseText);
            if (payload is not null && payload.Labels.Count > 0 && payload.Datasets.Count > 0)
                return payload;
        }
        catch (JsonException) { }

        return new AnalysisAgentPayload("bar", "Analysis", [], [new ChartDataset("Result", [])], responseText);
    }
}

public sealed record InvoiceAnalysisResult(
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("tools")] IReadOnlyList<string> Tools,
    [property: JsonPropertyName("chart")] ChartSpec Chart,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("usage")] InvoiceTokenUsage? Usage);

public sealed record ChartSpec(
    [property: JsonPropertyName("chartType")] string ChartType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("datasets")] IReadOnlyList<ChartDataset> Datasets);

public sealed record ChartDataset(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("data")] IReadOnlyList<double> Data);

internal sealed record AnalysisAgentPayload(
    [property: JsonPropertyName("chartType")] string ChartType,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("labels")] IReadOnlyList<string> Labels,
    [property: JsonPropertyName("datasets")] IReadOnlyList<ChartDataset> Datasets,
    [property: JsonPropertyName("summary")] string Summary);
