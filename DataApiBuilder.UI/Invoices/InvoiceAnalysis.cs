using System.Globalization;
using System.Text.Json;

namespace DataApiBuilder.UI.Invoices;

internal static class InvoiceAnalysis
{
    /// <summary>
    /// Groups rows by the options' group/series fields, computes the aggregate,
    /// applies the comparison filter, and returns the sorted result.
    /// Throws <see cref="InvalidOperationException"/> for invalid options (same messages the UI surfaces).
    /// </summary>
    public static List<AnalysisBucket> Aggregate(IReadOnlyList<JsonElement> rows, AggOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.GroupField))
            throw new InvalidOperationException("Choose a field to group by.");
        if (opts.Aggregate != Agg.Count && string.IsNullOrWhiteSpace(opts.MeasureField))
            throw new InvalidOperationException("Choose a numeric field to aggregate.");
        if (opts.Comparison != "none" && !TryParseNumber(opts.Threshold, out _))
            throw new InvalidOperationException("Enter a numeric comparison threshold.");

        return rows
            .GroupBy(r => new
            {
                Group = ToBucketLabel(GetCell(r, opts.GroupField)),
                Series = string.IsNullOrEmpty(opts.SeriesField) ? "" : ToBucketLabel(GetCell(r, opts.SeriesField)),
            })
            .Select(g =>
            {
                double value;
                if (opts.Aggregate == Agg.Count)
                {
                    value = g.Count();
                }
                else
                {
                    var nums = g
                        .Select(r => TryParseNumber(GetCell(r, opts.MeasureField), out var n) ? (double?)n : null)
                        .Where(n => n.HasValue).Select(n => n!.Value).ToList();
                    value = nums.Count == 0 ? 0 : opts.Aggregate switch
                    {
                        Agg.Sum => nums.Sum(),
                        Agg.Average => nums.Average(),
                        Agg.Min => nums.Min(),
                        Agg.Max => nums.Max(),
                        _ => 0,
                    };
                }
                return new AnalysisBucket(g.Key.Group, g.Key.Series, value, g.Count());
            })
            .Where(b => PassesComparison(b, opts.Comparison, opts.Threshold))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Group)
            .ThenBy(x => x.Series)
            .ToList();
    }

    /// <summary>
    /// Shapes aggregation buckets into the labels + datasets structure expected by Chart.js.
    /// </summary>
    public static (string[] Labels, ChartDataset[] Datasets) BuildDatasets(
        IReadOnlyList<AnalysisBucket> buckets, string aggLabel)
    {
        var labels = buckets.Select(x => x.Group).Distinct().ToArray();
        var datasets = buckets
            .GroupBy(x => x.Series)
            .Select(g => new ChartDataset(
                string.IsNullOrEmpty(g.Key) ? aggLabel : g.Key,
                labels.Select(label => g.FirstOrDefault(x => x.Group == label)?.Value ?? 0).ToArray()))
            .ToArray();
        return (labels, datasets);
    }

    /// <summary>
    /// Reads a cell value from a JSON row, handling dotted relation paths (e.g. "customer.name").
    /// Returns "—" for null/missing values.
    /// </summary>
    public static string GetCell(JsonElement row, string field)
    {
        if (field.Contains('.'))
        {
            var dot = field.IndexOf('.');
            if (!row.TryGetProperty(field[..dot], out var parent) || parent.ValueKind == JsonValueKind.Null) return "—";
            row = parent;
            field = field[(dot + 1)..];
        }
        return row.TryGetProperty(field, out var val)
            ? val.ValueKind is JsonValueKind.Null ? "—" : val.ToString()
            : "—";
    }

    public static bool TryParseNumber(string value, out double number) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number) ||
        double.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number);

    private static string ToBucketLabel(string value) =>
        string.IsNullOrWhiteSpace(value) || value == "—" ? "Unspecified" : value;

    private static bool PassesComparison(AnalysisBucket bucket, string comparison, string threshold)
    {
        if (comparison == "none") return true;
        TryParseNumber(threshold, out var t);
        return comparison switch
        {
            "gt"  => bucket.Value > t,
            "gte" => bucket.Value >= t,
            "lt"  => bucket.Value < t,
            "lte" => bucket.Value <= t,
            "eq"  => bucket.Value == t,
            _     => true,
        };
    }
}
