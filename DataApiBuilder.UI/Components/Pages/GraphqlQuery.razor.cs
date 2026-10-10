using System.Net.Http.Json;
using System.Text.Json;
using DataApiBuilder.UI.Invoices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DataApiBuilder.UI.Components.Pages;

public partial class GraphqlQuery
{
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] private DataApiBuilder.UI.SchemaCache SchemaCache { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<GraphqlQuery> Logger { get; set; } = default!;

    // Invoice report schema
    private const int ReportPageSize = 200;
    private const int ReportRowLimit = 5_000;
    private string _invoiceQueryName = "invoices";
    private List<FieldDef>? _fields;
    private string? _schemaError;
    private bool _reloading;

    // Query
    private readonly List<FilterRow> _filters = [];
    private string _orderField = "";
    private string _orderDir = "ASC";
    private bool _loading;
    private string? _error;
    private List<JsonElement>? _rows;
    private List<FieldDef> _selectedSnapshot = [];
    private int _loadedCount;
    private bool _reportIsComplete;
    private bool _reportReachedLimit;
    private bool _reportIsStale;

    // Result analysis
    private string _groupField = "";
    private string _seriesField = "";
    private string _measureField = "";
    private Agg _agg = Agg.Count;
    private string _comparison = "none";
    private string _threshold = "";
    private string _chartType = "bar";
    private List<AnalysisBucket>? _aggResult;
    private string _aggLabel = "";
    private string? _aggError;
    private bool _renderChart;

    private IEnumerable<FieldDef> AnalysisFields => _fields ?? [];
    private IEnumerable<FieldDef> NumericAnalysisFields => AnalysisFields.Where(f => f.Kind == FieldKind.Number);
    private IEnumerable<IGrouping<string, FieldDef>> DisplayFieldGroups => AnalysisFields.GroupBy(f => f.Category);

    private string AggregateDescription => _agg == Agg.Count
        ? "invoice count"
        : string.IsNullOrEmpty(_measureField) ? "a numeric measure" : $"{_agg.ToString().ToLowerInvariant()} {FindField(_measureField).Label}";

    private string AnalysisTitle => string.IsNullOrEmpty(_seriesField)
        ? $"{AggregateDescription} by {FindField(_groupField).Label}"
        : $"{AggregateDescription} by {FindField(_groupField).Label}, split by {FindField(_seriesField).Label}";

    private IEnumerable<AnalysisSuggestion> AnalysisSuggestions
    {
        get
        {
            var measure = NumericAnalysisFields.FirstOrDefault();
            var dimensions = AnalysisFields.Where(f => f.Kind is FieldKind.Text or FieldKind.Enum or FieldKind.Date).Take(6).ToList();
            if (dimensions.Count == 0) yield break;

            yield return new AnalysisSuggestion($"How many invoices are there by {dimensions[0].Label}?", dimensions[0].Name, null, Agg.Count, null);
            if (measure is not null)
            {
                foreach (var dimension in dimensions)
                    yield return new AnalysisSuggestion($"Compare total {measure.Label} by {dimension.Label}", dimension.Name, null, Agg.Sum, measure.Name);
                if (dimensions.Count > 1)
                    yield return new AnalysisSuggestion($"Compare average {measure.Label} by {dimensions[0].Label}, split by {dimensions[1].Label}", dimensions[0].Name, dimensions[1].Name, Agg.Average, measure.Name);
            }
        }
    }

    protected override async Task OnInitializedAsync() => await LoadSchema(false);

    private async Task ReloadSchema()
    {
        _reloading = true;
        _fields = null;
        _schemaError = null;
        _rows = null;
        _error = null;
        _filters.Clear();
        _loadedCount = 0;
        _reportIsComplete = false;
        _reportReachedLimit = false;
        _reportIsStale = false;
        ClearAnalysis();
        try   { await LoadSchema(true); }
        finally { _reloading = false; }
    }

    private async Task LoadSchema(bool forceRefresh)
    {
        try
        {
            using var doc = forceRefresh
                ? await SchemaCache.RefreshAsync()
                : await SchemaCache.GetAsync();

            var (queryName, fields) = InvoiceSchema.Parse(doc!);
            _invoiceQueryName = queryName;
            _fields = fields;
            ResetAnalysis();
        }
        catch (Exception ex) { _schemaError = ex.Message; }
    }

    private async Task RunQuery()
    {
        _error = null;
        _rows = null;
        _loadedCount = 0;
        _reportIsComplete = false;
        _reportReachedLimit = false;
        _reportIsStale = false;
        ClearAnalysis();
        _loading = true;
        try
        {
            _selectedSnapshot = _fields!.Where(f => f.Selected).ToList();
            if (_selectedSnapshot.Count == 0)
                throw new InvalidOperationException("Choose at least one report column.");

            var client = HttpClientFactory.CreateClient("dab");
            var rows = new List<JsonElement>();
            string? after = null;
            bool hasNextPage;

            do
            {
                var gql = InvoiceQuery.Build(_fields!, _filters, _orderField, _orderDir, _invoiceQueryName, ReportPageSize, after);
                Logger.LogInformation("GraphQL query (after={After}):\n{Query}", after, gql);
                using var resp = await client.PostAsJsonAsync("graphql", new { query = gql });
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");

                using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
                if (doc is null) throw new InvalidOperationException("The report service returned an empty response.");
                if (doc.RootElement.TryGetProperty("errors", out var errors))
                    throw new InvalidOperationException(errors[0].GetProperty("message").GetString() ?? "GraphQL error");

                var connection = doc.RootElement.GetProperty("data").GetProperty(_invoiceQueryName);
                var pageRows = connection.GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToList();
                rows.AddRange(pageRows.Take(ReportRowLimit - rows.Count));
                _loadedCount = rows.Count;
                hasNextPage = connection.GetProperty("hasNextPage").GetBoolean();
                after = connection.TryGetProperty("endCursor", out var cursor) && cursor.ValueKind != JsonValueKind.Null
                    ? cursor.GetString()
                    : null;

                if (rows.Count == ReportRowLimit && hasNextPage)
                {
                    _reportReachedLimit = true;
                    break;
                }

                if (hasNextPage && (string.IsNullOrEmpty(after) || pageRows.Count == 0))
                    throw new InvalidOperationException("The report service did not provide a usable cursor for the next page.");

                await InvokeAsync(StateHasChanged);
            }
            while (hasNextPage);

            _rows = rows;
            _reportIsComplete = !_reportReachedLimit;
            _reportIsStale = false;
            if (_reportIsComplete)
                ResetAnalysis();
        }
        catch (Exception ex)
        {
            _rows = null;
            _error = ex.Message;
        }
        finally { _loading = false; }
    }

    private void Analyze()
    {
        _aggError = null;
        _aggResult = null;
        try
        {
            _aggLabel = _agg == Agg.Count ? "Count" : $"{_agg} of {FindField(_measureField).Label}";
            var opts = new AggOptions(_groupField, _seriesField, _agg, _measureField, _comparison, _threshold);
            var buckets = InvoiceAnalysis.Aggregate(_rows!, opts);
            _aggResult = buckets;
            _renderChart = buckets.Count > 0;
        }
        catch (Exception ex) { _aggError = ex.Message; _aggResult = null; }
    }

    private void ResetAnalysis()
    {
        ClearAnalysis();
        var fields = AnalysisFields.ToList();
        _groupField = fields.FirstOrDefault(f => f.Kind is FieldKind.Text or FieldKind.Enum)?.Name
                      ?? fields.FirstOrDefault()?.Name ?? "";
        _measureField = fields.FirstOrDefault(f => f.Kind == FieldKind.Number)?.Name ?? "";
        _agg = string.IsNullOrEmpty(_measureField) ? Agg.Count : Agg.Sum;
        _comparison = "none";
        _threshold = "";
        _chartType = "bar";
    }

    private void ApplySuggestion(AnalysisSuggestion suggestion)
    {
        _groupField = suggestion.GroupField;
        _seriesField = suggestion.SeriesField ?? "";
        _agg = suggestion.Aggregate;
        _measureField = suggestion.MeasureField ?? "";
        _comparison = "none";
        _threshold = "";
        _chartType = "bar";
        Analyze();
    }

    private void ClearAnalysis()
    {
        _aggResult = null;
        _aggError = null;
        _renderChart = false;
        _seriesField = "";
    }

    private void EnsureMeasureField()
    {
        if (_agg != Agg.Count && !NumericAnalysisFields.Any(f => f.Name == _measureField))
            _measureField = NumericAnalysisFields.FirstOrDefault()?.Name ?? "";
    }

    private void OnFilterFieldChanged(FilterRow filter)
    {
        ResetOp(filter);
        MarkReportStale();
    }

    private void ResetOp(FilterRow f) => f.Op = InvoiceQuery.GetOps(FindField(f.Field))[0];

    private void RemoveFilter(int index)
    {
        _filters.RemoveAt(index);
        MarkReportStale();
    }

    private void MarkReportStale()
    {
        if (_rows is null) return;
        _reportIsStale = true;
        ClearAnalysis();
    }

    private void AddFilter()
    {
        var first = _fields!.First(f => !f.Name.Contains('.'));
        _filters.Add(new FilterRow { Field = first.Name, Op = InvoiceQuery.GetOps(first)[0] });
        MarkReportStale();
    }

    private FieldDef FindField(string name) =>
        _fields!.Find(f => f.Name == name) ?? _fields[0];

    // Shims so markup can call these without qualifying the namespace
    private static string GetCell(JsonElement row, string field) => InvoiceAnalysis.GetCell(row, field);
    private static string[] GetOps(FieldDef f) => InvoiceQuery.GetOps(f);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_renderChart && _aggResult is not null)
        {
            _renderChart = false;
            var (labels, datasets) = InvoiceAnalysis.BuildDatasets(_aggResult, _aggLabel);
            await JS.InvokeVoidAsync("renderAnalysisChart", "cmp-chart", _chartType, labels, datasets, _aggLabel);
        }
    }
}
