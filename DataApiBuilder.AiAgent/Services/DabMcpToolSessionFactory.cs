using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace DataApiBuilder.AiAgent.Services;

public sealed class DabMcpToolSessionFactory
{
    private readonly DabMcpOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public DabMcpToolSessionFactory(IOptions<DabMcpOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    public async Task<DabMcpToolSession> OpenAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("DabMcp__Endpoint must be an absolute HTTP or HTTPS URL ending in /mcp.");
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.AutoDetect,
                Name = "dab-invoice-mcp"
            },
            _loggerFactory);

        var client = await McpClient.CreateAsync(transport, null, _loggerFactory, cancellationToken);
        var availableTools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var tools = availableTools
            .Cast<AITool>()
            .ToArray();

        if (tools.Length == 0)
        {
            await client.DisposeAsync();
            throw new InvalidOperationException("No tools were discovered from the DAB MCP endpoint.");
        }

        return new DabMcpToolSession(client, tools);
    }
}

public sealed class DabMcpToolSession : IAsyncDisposable
{
    private readonly McpClient _client;

    public DabMcpToolSession(McpClient client, IReadOnlyList<AITool> tools)
    {
        _client = client;
        Tools = tools;
    }

    public IReadOnlyList<AITool> Tools { get; }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}

public sealed class DabMcpToolSessionProvider : IAsyncDisposable
{
    private readonly DabMcpToolSessionFactory _factory;
    private DabMcpToolSession? _session;

    public DabMcpToolSessionProvider(DabMcpToolSessionFactory factory)
    {
        _factory = factory;
    }

    public DabMcpToolSession Session => _session
        ?? throw new InvalidOperationException("DAB MCP tools have not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return;
        }

        _session = await _factory.OpenAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _session?.DisposeAsync() ?? ValueTask.CompletedTask;
}