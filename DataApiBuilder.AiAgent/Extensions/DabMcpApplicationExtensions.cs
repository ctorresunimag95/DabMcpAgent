using DataApiBuilder.AiAgent.Services;

namespace DataApiBuilder.AiAgent.Extensions;

public static class DabMcpApplicationExtensions
{
    public static async Task InitializeDabMcpToolsAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        await app.Services
            .GetRequiredService<DabMcpToolSessionProvider>()
            .InitializeAsync(cancellationToken);
    }
}