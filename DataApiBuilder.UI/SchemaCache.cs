using System.Text.Json;

namespace DataApiBuilder.UI;

public sealed class SchemaCache(IHttpClientFactory http, IWebHostEnvironment env)
{
    private readonly string _path = Path.Combine(env.ContentRootPath, "schema-cache.json");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _json;

    public async Task<JsonDocument> GetAsync()
    {
        if (_json is not null) return JsonDocument.Parse(_json);

        await _lock.WaitAsync();
        try
        {
            if (_json is not null) return JsonDocument.Parse(_json);

            if (File.Exists(_path))
                _json = await File.ReadAllTextAsync(_path);
            else
                _json = await FetchAndSaveAsync();

            return JsonDocument.Parse(_json);
        }
        finally { _lock.Release(); }
    }

    public async Task<JsonDocument> RefreshAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _json = await FetchAndSaveAsync();
            return JsonDocument.Parse(_json);
        }
        finally { _lock.Release(); }
    }

    private async Task<string> FetchAndSaveAsync()
    {
        var client = http.CreateClient("dab");
        using var resp = await client.PostAsJsonAsync("graphql", new { query = Introspection });
        var json = await resp.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(_path, json);
        return json;
    }

    public const string Introspection = """
        {
          __schema {
            queryType {
              fields {
                name
                type { kind name
                  ofType { kind name
                    ofType { kind name
                      ofType { kind name
                        ofType { kind name } } } } }
              }
            }
            types {
              name kind
              fields {
                name
                type { kind name
                  ofType { kind name
                    ofType { kind name
                      ofType { kind name } } } }
              }
              enumValues { name }
            }
          }
        }
        """;
}
