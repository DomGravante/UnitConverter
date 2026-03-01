using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitConverter.Pages;

/// <summary>
/// Parses and evaluates mixed-unit arithmetic expressions like "3in + 1mm - 0.010in".
/// Supported operators: + and -
/// Supported token formats: decimal (3in), pure fraction (3/8in), mixed fraction (1 3/4in)
/// Units: in/inch/inches and mm/millimeter/millimeters
/// </summary>
internal static partial class ExpressionParser
{
    private const double MmPerInch = 25.4;

    // Matches a single value+unit token. Alternation order is significant:
    // mixed fraction is tried first so "1 3/4in" isn't split into "1" and "3/4in".
    //
    // Group index reference:
    //   1 = full value text
    //   2 = whole      (mixed fraction: "1" in "1 3/4")
    //   3 = numerator  (mixed fraction: "3" in "1 3/4")
    //   4 = denominator(mixed fraction: "4" in "1 3/4")
    //   5 = numerator  (pure fraction:  "3" in "3/8")
    //   6 = denominator(pure fraction:  "8" in "3/8")
    //   7 = decimal value
    //   8 = unit string
    [GeneratedRegex(
        @"((\d+)\s+(\d+)/(\d+)|(\d+)/(\d+)|([\d.]+))\s*(in(?:ch(?:es?)?)?|mm(?:illimeters?)?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    /// <summary>
    /// Attempts to parse <paramref name="query"/> as a mixed-unit arithmetic expression.
    /// Returns false if the query is not an expression (fewer than 2 unit tokens found,
    /// or unexpected characters between tokens).
    /// </summary>
    internal static bool TryParse(string query, out double resultMm, out string label)
    {
        resultMm = 0;
        label = string.Empty;

        var matches = TokenRegex().Matches(query);
        if (matches.Count < 2)
            return false;

        // Text before the first token: allow empty or a single leading "-" (negation)
        string prefix = query[..matches[0].Index].Trim();
        if (prefix != string.Empty && prefix != "-")
            return false;

        // Text after the last token: must be empty
        var last = matches[matches.Count - 1];
        if (query[(last.Index + last.Length)..].Trim().Length > 0)
            return false;

        // Extract and validate the operator between each consecutive pair of tokens
        char[] ops = new char[matches.Count - 1];
        for (int i = 0; i < ops.Length; i++)
        {
            int start = matches[i].Index + matches[i].Length;
            int end = matches[i + 1].Index;
            string between = query[start..end].Trim();
            if (between != "+" && between != "-")
                return false;
            ops[i] = between[0];
        }

        // Parse the first token, apply leading negation if present
        if (!TryTokenMm(matches[0], out double firstMm))
            return false;
        double total = prefix == "-" ? -firstMm : firstMm;

        // Accumulate the rest
        for (int i = 1; i < matches.Count; i++)
        {
            if (!TryTokenMm(matches[i], out double tokenMm))
                return false;
            total += ops[i - 1] == '+' ? tokenMm : -tokenMm;
        }

        resultMm = total;

        // Build a normalized human-readable label
        var sb = new StringBuilder();
        if (prefix == "-") sb.Append('-');
        sb.Append(TokenLabel(matches[0]));
        for (int i = 0; i < ops.Length; i++)
        {
            sb.Append(' ');
            sb.Append(ops[i]);
            sb.Append(' ');
            sb.Append(TokenLabel(matches[i + 1]));
        }
        label = sb.ToString();

        return true;
    }

    private static bool TryTokenMm(Match m, out double mm)
    {
        mm = 0;
        bool metric = m.Groups[8].Value.StartsWith("mm", StringComparison.OrdinalIgnoreCase);

        double value;
        if (m.Groups[2].Success) // mixed fraction
        {
            if (!double.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double whole)
                || !double.TryParse(m.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double num)
                || !double.TryParse(m.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double den)
                || den == 0)
                return false;
            value = whole + num / den;
        }
        else if (m.Groups[5].Success) // pure fraction
        {
            if (!double.TryParse(m.Groups[5].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double num)
                || !double.TryParse(m.Groups[6].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double den)
                || den == 0)
                return false;
            value = num / den;
        }
        else // decimal
        {
            if (!double.TryParse(m.Groups[7].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
        }

        mm = metric ? value : value * MmPerInch;
        return true;
    }

    private static string TokenLabel(Match m)
    {
        bool metric = m.Groups[8].Value.StartsWith("mm", StringComparison.OrdinalIgnoreCase);
        string unit = metric ? "mm" : "in";

        if (m.Groups[2].Success) return $"{m.Groups[2].Value} {m.Groups[3].Value}/{m.Groups[4].Value} {unit}";
        if (m.Groups[5].Success) return $"{m.Groups[5].Value}/{m.Groups[6].Value} {unit}";
        return $"{m.Groups[7].Value} {unit}";
    }
}
