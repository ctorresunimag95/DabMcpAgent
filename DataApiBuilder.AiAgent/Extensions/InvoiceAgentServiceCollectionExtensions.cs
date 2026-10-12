using Azure;
using Azure.AI.OpenAI;
using DataApiBuilder.AiAgent.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

namespace DataApiBuilder.AiAgent.Extensions;

public static class InvoiceAgentServiceCollectionExtensions
{
    public const string InvoiceAgentName = "invoice-agent";
    public const string AnalysisAgentName = "analysis-agent";

    private const string Instructions = """
        You are an assistant with expertise in the invoice business domain.
        You have access to the `sql-mcp-server` tool, which exposes invoice data through Data API Builder (DAB) via MCP.

        Data access rules:
        1. Use `sql-mcp-server` for invoice data retrieval.
        2. Available entity: Invoice. It is a flat table containing invoice, customer, and sales representative fields.
        3. When calling read_records, use only exact column names listed in the schema below.
        4. Never create, infer, alias, or hallucinate column names.
        5. If a user asks for a field that does not exist, explain and proceed with the closest valid fields.
        6. Do not perform write/update/delete operations; this tool is read-only.

        SQL MCP Tools:
        - Use `describe_entities` to discover available entities and operations.
        - Use `read_records` to retrieve records based on filters and projections.

        Schemas:
        - Invoice: InvoiceId, CustomerId, CustomerName, SalesRepId, SalesRepName, SalesRepRegion, Amount, Currency, Status, InvoiceDate, DueDate, PaymentMethod

        Query behavior:
        - Prefer precise filtering and projection; return only relevant records and fields.
        - If user intent is ambiguous, ask a short clarification question before querying.

        Response:
        - Answer in clear, concise plain text.
        - Do not produce JSON, markdown tables, or any structured output.
        - Keep your answer to two or three sentences unless more detail is clearly needed.
        """;

    private const string AnalysisInstructions = """
        You are a data analyst with expertise in the invoice business domain.
        You have access to the `sql-mcp-server` tool, which exposes invoice data through Data API Builder (DAB) via MCP.

        Data access rules:
        1. Use `sql-mcp-server` for invoice data retrieval.
        2. Available entity: Invoice. It is a flat table containing invoice, customer, and sales representative fields.
        3. When calling read_records, use only exact column names listed in the schema below.
        4. Never create, infer, alias, or hallucinate column names.
        5. Do not perform write/update/delete operations; this tool is read-only.

        SQL MCP Tools:
        - Use `describe_entities` to discover available entities and operations.
        - Use `read_records` to retrieve records based on filters and projections.

        Schemas:
        - Invoice: InvoiceId, CustomerId, CustomerName, SalesRepId, SalesRepName, SalesRepRegion, Amount, Currency, Status, InvoiceDate, DueDate, PaymentMethod

        Query behavior:
        - Retrieve and aggregate real data via MCP tools. Never fabricate numbers.
        - Group and aggregate data as needed to produce a meaningful chart.
        - If user intent is ambiguous, ask a short clarification question before querying.

        Response contract:
        - Always return exactly one valid JSON object, with no markdown fence or surrounding text.
        - Required fields: chartType, title, labels, datasets, summary.
        - chartType must be one of: "bar", "pie", "line".
          Choose "bar" for comparisons, "pie" for distributions/proportions, "line" for trends over time.
          Honor the user's explicit request if given.
        - labels is an array of strings (category names, dates, or group names).
        - datasets is an array of objects, each with a "label" string and a "data" array of numbers.
        - labels and each datasets[].data array must have the same length.
        - title is a short, descriptive chart title.
        - summary is two sentences maximum of plain text explaining the key insight from the chart.
        - Include only data retrieved through MCP tools.

        Example structure (do not copy values):
        {"chartType":"bar","title":"Total Amount by Payment Method","labels":["Credit Card","Bank Transfer"],"datasets":[{"label":"Total Amount","data":[12000.50,8500.00]}],"summary":"Credit card is the dominant payment method. Bank transfers account for roughly 40% of total revenue."}
        """;

    public static IServiceCollection AddInvoiceAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureFoundryOptions>(configuration.GetSection("AzureFoundry"));
        services.Configure<DabMcpOptions>(configuration.GetSection("DabMcp"));

        // var azureFoundryOptions = configuration.GetSection("AzureFoundry").Get<AzureFoundryOptions>() ?? new AzureFoundryOptions();
        // if (!string.IsNullOrWhiteSpace(azureFoundryOptions.Endpoint) &&
        //     !string.IsNullOrWhiteSpace(azureFoundryOptions.ApiKey) &&
        //     !string.IsNullOrWhiteSpace(azureFoundryOptions.DeploymentName))
        // {
        //     services.AddSingleton(sp =>
        //     {
        //         var client = new AzureOpenAIClient(
        //             new Uri(azureFoundryOptions.Endpoint),
        //             new AzureKeyCredential(azureFoundryOptions.ApiKey));

        //         return client
        //             .GetChatClient(azureFoundryOptions.DeploymentName)
        //             .AsIChatClient()
        //             .AsBuilder()
        //             .UseOpenTelemetry(sp.GetRequiredService<ILoggerFactory>(), "DataApiBuilder.AiAgent")
        //             .Build();
        //     });
        // }

        services.AddSingleton(sp =>
        {
            var endpoint = configuration.GetValue<string>("Ollama:Endpoint");
            var model = configuration.GetValue<string>("Ollama:Model");

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var ollamaUri))
            {
                throw new InvalidOperationException(
                    "Ollama:Endpoint must be an absolute HTTP or HTTPS URL.");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException(
                    "Ollama:Model must identify an Ollama model.");
            }

            IChatClient chatClient = new OllamaSharp.OllamaApiClient(ollamaUri, model);
            return chatClient
                .AsBuilder()
                .UseOpenTelemetry(
                    sp.GetRequiredService<ILoggerFactory>(),
                    "DataApiBuilder.AiAgent")
                .Build();
        });

        services.AddSingleton<DabMcpToolSessionFactory>();
        services.AddSingleton<DabMcpToolSessionProvider>();

        services.AddAIAgent(InvoiceAgentName, Instructions);
        services.AddAIAgent(AnalysisAgentName, AnalysisInstructions);
        services.AddSingleton<InvoiceQueryService>();
        services.AddSingleton<InvoiceAnalysisService>();

        return services;
    }
}
