namespace RecallGraph.Infrastructure;

public static class IdentifierNormalizer
{
    public static string NormalizeLot(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var v = value.Trim().ToUpperInvariant();
        // Conservative: trim and collapse whitespace only. Do not map O/0, I/1 or punctuation.
        while (v.Contains("  ", StringComparison.Ordinal)) v = v.Replace("  ", " ");
        return v;
    }

    public static string NearMatchKey(string? value)
    {
        var v = NormalizeLot(value);
        // For warning detection only, visually confusable characters are treated as equivalent.
        return v
            .Replace('O', '0')
            .Replace('I', '1')
            .Replace('L', '1')
            .Replace("-", "")
            .Replace(" ", "");
    }

    public static bool IsNearMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        var na = NormalizeLot(a);
        var nb = NormalizeLot(b);
        if (na == nb) return false;
        return NearMatchKey(a) == NearMatchKey(b);
    }

    public static decimal? ParseQuantity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Replace(",", "").Trim();
        return decimal.TryParse(v, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(value, out d)) return d;
        return null;
    }
}
