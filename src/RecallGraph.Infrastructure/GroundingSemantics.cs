using System.Text.RegularExpressions;

namespace RecallGraph.Infrastructure;

/// <summary>
/// Shared, deliberately narrow rules for deciding whether a source quote actually contains the
/// value it is supposed to support. These rules are used by extraction, tests and UI-facing
/// status labels so "quote verified" cannot quietly mean "identifier appeared somewhere else".
/// </summary>
public static class GroundingSemantics
{
    public const string QuoteVerified = "quote_verified";
    public const string AdjacentLineContext = "context_match_value_adjacent";
    public const string IdentifierUnverified = "identifier_matched_quote_unverified";
    public const string UnverifiedSource = "needs_review_unverified_source";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// True only when the raw value appears inside the quote span after collapse-whitespace and
    /// case-insensitive comparison. Identifier-like values use token boundaries so RM-0873 does
    /// not match inside RM-08730 or RM-O873. Pure numbers cannot match the prefix of a decimal.
    /// </summary>
    public static bool ValueAppearsInQuote(string? rawValue, string? quote)
    {
        var value = Normalize(rawValue);
        var span = Normalize(quote);
        if (value.Length == 0 || span.Length == 0) return false;

        var isIdentifierLike = Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9 _./-]*$")
            && value.Length <= 80
            && (Regex.IsMatch(value, @"\d") || value.All(c => char.IsUpper(c) || char.IsDigit(c) || c is '-' or '_' or '.' or '/'));

        if (!isIdentifierLike)
        {
            return span.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        var pattern = $@"(?<![A-Za-z0-9]){Regex.Escape(value)}(?![A-Za-z0-9])";
        if (Regex.IsMatch(value, @"^\d+(?:[.,]\d+)?$"))
        {
            pattern = $@"(?<![\d.,]){Regex.Escape(value)}(?![\d.,])";
        }
        return Regex.IsMatch(span, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool QuoteAppearsInLine(string? quote, string? line)
    {
        var span = Normalize(quote);
        var text = Normalize(line);
        if (span.Length == 0 || text.Length == 0) return false;
        return text.Contains(span, StringComparison.OrdinalIgnoreCase);
    }
}
