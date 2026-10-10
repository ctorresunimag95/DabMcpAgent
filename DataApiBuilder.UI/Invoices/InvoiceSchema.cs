using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataApiBuilder.UI.Invoices;

internal static class InvoiceSchema
{
    private const string InvoiceEntityName = "Invoice";

    /// <summary>
    /// Parses a DAB GraphQL introspection response and returns the invoice query name
    /// and the list of displayable field definitions. Throws if the Invoice entity
    /// cannot be found in the schema.
    /// </summary>
    public static (string QueryName, List<FieldDef> Fields) Parse(JsonDocument schemaDoc)
    {
        var schema = schemaDoc.RootElement.GetProperty("data").GetProperty("__schema");

        // Build type lookup (clone so storage survives doc disposal)
        var typeMap = new Dictionary<string, JsonElement>();
        foreach (var t in schema.GetProperty("types").EnumerateArray())
        {
            var n = t.GetProperty("name").GetString()!;
            if (!n.StartsWith("__"))
                typeMap[n] = t.Clone();
        }

        foreach (var qf in schema.GetProperty("queryType").GetProperty("fields").EnumerateArray())
        {
            var queryName = qf.GetProperty("name").GetString()!;
            if (queryName.StartsWith("__")) continue;

            // Unwrap query field type → connection object name
            var connName = UnwrapTypeName(qf.GetProperty("type"));
            if (connName is null || !typeMap.TryGetValue(connName, out var connType)) continue;

            // Find the "items" field in the connection type
            if (!connType.TryGetProperty("fields", out var connFields) ||
                connFields.ValueKind == JsonValueKind.Null) continue;

            var itemsField = connFields.EnumerateArray()
                .FirstOrDefault(f => f.GetProperty("name").GetString() == "items");
            if (itemsField.ValueKind == JsonValueKind.Undefined) continue;

            // Unwrap items → entity type name
            var entityTypeName = UnwrapTypeName(itemsField.GetProperty("type"));
            if (entityTypeName is null || !typeMap.TryGetValue(entityTypeName, out var entityType)) continue;
            if (!string.Equals(entityTypeName, InvoiceEntityName, StringComparison.OrdinalIgnoreCase)) continue;

            if (!entityType.TryGetProperty("fields", out var entityFields) ||
                entityFields.ValueKind == JsonValueKind.Null) continue;

            var fieldDefs = new List<FieldDef>();
            foreach (var ef in entityFields.EnumerateArray())
            {
                var fname = ef.GetProperty("name").GetString()!;
                if (IsTechnicalField(fname)) continue;
                var baseType = UnwrapTypeName(ef.GetProperty("type"));
                bool expanded = false;

                if (baseType is not null)
                {
                    if (typeMap.TryGetValue(baseType, out var ft))
                    {
                        var ftKind = ft.GetProperty("kind").GetString();
                        if (ftKind == "OBJECT" &&
                                 ft.TryGetProperty("fields", out var relFields) &&
                                 relFields.ValueKind != JsonValueKind.Null)
                        {
                            var rfList = relFields.EnumerateArray().ToList();
                            if (!rfList.Any(rf => rf.GetProperty("name").GetString() == "items"))
                            {
                                expanded = true;
                                foreach (var rf in rfList)
                                {
                                    var rname = rf.GetProperty("name").GetString()!;
                                    if (IsTechnicalField(rname)) continue;
                                    var rbase = UnwrapTypeName(rf.GetProperty("type"));
                                    if (rbase is null || !IsScalarOrEnum(rbase, typeMap)) continue;
                                    fieldDefs.Add(new FieldDef
                                    {
                                        Name = $"{fname}.{rname}",
                                        Label = $"{ToLabel(fname)} {ToLabel(rname)}",
                                        Category = ToCategory(fname),
                                        Kind = GetFieldKind(rbase, typeMap),
                                        EnumValues = GetEnumValues(rbase, typeMap),
                                    });
                                }
                            }
                        }
                    }
                }

                if (!expanded)
                {
                    if (baseType is null || !IsScalarOrEnum(baseType, typeMap)) continue;
                    fieldDefs.Add(new FieldDef
                    {
                        Name = fname,
                        Label = ToLabel(fname),
                        Category = "Invoice details",
                        Kind = GetFieldKind(baseType, typeMap),
                        EnumValues = GetEnumValues(baseType, typeMap),
                    });
                }
            }

            if (fieldDefs.Count == 0) continue;
            return (queryName, fieldDefs);
        }

        throw new InvalidOperationException("The GraphQL schema does not expose a queryable Invoice entity.");
    }

    // Walk NON_NULL / LIST wrappers to reach the named base type.
    private static string? UnwrapTypeName(JsonElement type)
    {
        while (true)
        {
            var kind = type.GetProperty("kind").GetString();
            if (kind is "SCALAR" or "OBJECT" or "ENUM" or "INPUT_OBJECT")
                return type.GetProperty("name").GetString();
            if (!type.TryGetProperty("ofType", out var inner) || inner.ValueKind == JsonValueKind.Null)
                return null;
            type = inner;
        }
    }

    internal static string ToLabel(string name)
    {
        var pascal = char.ToUpper(name[0]) + name[1..];
        return Regex.Replace(pascal, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }

    private static string ToCategory(string relationshipName) =>
        string.Equals(relationshipName, "customer", StringComparison.OrdinalIgnoreCase) ? "Customer" : ToLabel(relationshipName);

    private static bool IsTechnicalField(string name) =>
        string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Id", StringComparison.Ordinal);

    private static bool IsScalarOrEnum(string typeName, IReadOnlyDictionary<string, JsonElement> typeMap) =>
        !typeMap.TryGetValue(typeName, out var type) || type.GetProperty("kind").GetString() is "SCALAR" or "ENUM";

    private static FieldKind GetFieldKind(string typeName, IReadOnlyDictionary<string, JsonElement> typeMap)
    {
        if (typeMap.TryGetValue(typeName, out var type) && type.GetProperty("kind").GetString() == "ENUM")
            return FieldKind.Enum;
        return typeName switch
        {
            "Int" or "Float" or "Long" or "Decimal" or "Byte" or "Short" or "Single" => FieldKind.Number,
            "DateTime" or "Date" or "DateTimeOffset" or "DateOnly" => FieldKind.Date,
            _ => FieldKind.Text,
        };
    }

    private static string[] GetEnumValues(string typeName, IReadOnlyDictionary<string, JsonElement> typeMap) =>
        typeMap.TryGetValue(typeName, out var type) && type.GetProperty("kind").GetString() == "ENUM"
            ? type.GetProperty("enumValues").EnumerateArray().Select(v => v.GetProperty("name").GetString()!).ToArray()
            : [];
}
