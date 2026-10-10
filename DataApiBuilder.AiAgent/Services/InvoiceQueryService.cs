using DataApiBuilder.AiAgent.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json.Serialization;

namespace DataApiBuilder.AiAgent.Services;

public sealed class InvoiceQueryService
{
    private readonly AIAgent _agent;
    private readonly DabMcpToolSessionProvider _mcpTools;

    public InvoiceQueryService(IServiceProvider services, DabMcpToolSessionProvider mcpTools)
    {
        _agent = services.GetRequiredKeyedService<AIAgent>(InvoiceAgentServiceCollectionExtensions.InvoiceAgentName);
        _mcpTools = mcpTools;
    }

    public async Task<InvoiceQueryResult> QueryAsync(string text, CancellationToken cancellationToken)
    {
        var mcpSession = _mcpTools.Session;
        var response = await _agent.RunAsync(
            text,
            options: new ChatClientAgentRunOptions(
                new ChatOptions
                {
                    Tools = mcpSession.Tools.ToList(),
                    MaxOutputTokens = 900
                }),
            cancellationToken: cancellationToken);

        var usedTools = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(functionCall => functionCall.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var usage = response.Usage;
        return new InvoiceQueryResult(
            "1.0",
            usedTools,
            response.Text,
            new InvoiceTokenUsage(
                usage?.InputTokenCount,
                usage?.OutputTokenCount,
                usage?.TotalTokenCount));
    }
}

public sealed record InvoiceQueryResult(
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("tools")] IReadOnlyList<string> Tools,
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("usage")] InvoiceTokenUsage? Usage);

public sealed record InvoiceTokenUsage(
    [property: JsonPropertyName("inputTokens")] long? InputTokens,
    [property: JsonPropertyName("outputTokens")] long? OutputTokens,
    [property: JsonPropertyName("totalTokens")] long? TotalTokens);
