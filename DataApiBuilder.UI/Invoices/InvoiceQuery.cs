using System.Globalization;
using System.Text.Json;

namespace DataApiBuilder.UI.Invoices;

internal static class InvoiceQuery
{
    /// <summary>
    /// Builds a paginated GraphQL query string for the invoice entity.
    /// </summary>
    public static string Build(
        IReadOnlyList<FieldDef> fields,
        IReadOnlyList<FilterRow> filters,
        string orderField,
        string orderDir,
        string queryName,
        int pageSize,
        string? after)
    {
        // Request every discovered scalar field so table column choices never limit analysis choices.
        var plain = fields.Where(f => !f.Name.Contains('.')).Select(f => f.Name);
        var nested = fields
            .Where(f => f.Name.Contains('.'))
            .GroupBy(f => f.Name.Split('.')[0])
            .Select(g => $"{g.Key} {{ {string.Join(" ", g.Select(f => f.Name.Split('.')[1]))} }}");
        var fieldList = string.Join(" ", plain.Concat(nested));
        var args = new List<string>();

        var active = filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
        if (active.Count > 0)
        {
            // Build one clause per filter, then group nested (dotted) filters by their relation
            // prefix so multiple filters on the same relation merge into one nested object.
            string Clause(FilterRow f, string leaf)
            {
                var fd = FindField(fields, f.Field);
                var val = ToGraphQlValue(fd, f.Value);
                return $"{leaf}: {{ {f.Op}: {val} }}";
            }

            var parts = active
                .GroupBy(f => f.Field.Contains('.') ? f.Field.Split('.')[0] : "")
                .Select(g => g.Key == ""
                    ? string.Join(", ", g.Select(f => Clause(f, f.Field)))
                    : $"{g.Key}: {{ {string.Join(", ", g.Select(f => Clause(f, f.Field.Split('.')[1])))} }}");

            args.Add($"filter: {{ {string.Join(", ", parts)} }}");
        }

        if (!string.IsNullOrEmpty(orderField))
            args.Add($"orderBy: {{ {orderField}: {orderDir} }}");

        args.Add($"first: {pageSize}");
        if (!string.IsNullOrEmpty(after))
            args.Add($"after: {JsonSerializer.Serialize(after)}");

        return $"{{ {queryName}({string.Join(", ", args)}) {{ items {{ {fieldList} }} endCursor hasNextPage }} }}";
    }

    public static string[] GetOps(FieldDef f) => f.Kind switch
    {
        FieldKind.Number or FieldKind.Date => ["eq", "neq", "gt", "gte", "lt", "lte"],
        FieldKind.Enum => ["eq", "neq"],
        _ => ["eq", "neq", "contains", "startsWith", "endsWith"],
    };

    private static FieldDef FindField(IReadOnlyList<FieldDef> fields, string name) =>
        fields.FirstOrDefault(f => f.Name == name) ?? fields[0];

    private static string ToGraphQlValue(FieldDef field, string value)
    {
        if (field.Kind == FieldKind.Number && InvoiceAnalysis.TryParseNumber(value, out var number))
            return number.ToString(CultureInfo.InvariantCulture);

        // DAB GraphQL enum filters require enum literals rather than quoted strings.
        if (field.Kind == FieldKind.Enum)
            return value;

        return JsonSerializer.Serialize(value);
    }
}
