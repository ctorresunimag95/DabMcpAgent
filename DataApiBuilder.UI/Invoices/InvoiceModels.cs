namespace DataApiBuilder.UI.Invoices;

internal enum FieldKind { Text, Number, Date, Enum }

internal sealed class FieldDef
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string Category { get; init; }
    public required FieldKind Kind { get; init; }
    public required string[] EnumValues { get; init; }
    public bool Selected { get; set; } = true;
}

internal sealed class FilterRow
{
    public string Field { get; set; } = "";
    public string Op { get; set; } = "eq";
    public string Value { get; set; } = "";
}

internal enum Agg { Count, Sum, Average, Min, Max }

internal sealed record AnalysisBucket(string Group, string Series, double Value, int RowCount);

internal sealed record ChartDataset(string Label, double[] Data);

internal sealed record AnalysisSuggestion(string Question, string GroupField, string? SeriesField, Agg Aggregate, string? MeasureField);

internal sealed record AggOptions(
    string GroupField,
    string SeriesField,
    Agg Aggregate,
    string MeasureField,
    string Comparison,
    string Threshold);
