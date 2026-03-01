using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Windows.UI.ViewManagement;

namespace UnitConverter.Pages;

internal sealed partial class UnitConverterPage : DynamicListPage
{
    private const double MmPerInch = 25.4;
    private IListItem[] _items = [];
    private string _currentQuery = "";

    // Shared icon for all result tiles
    private static readonly IconInfo RulerIcon = new("\uE8EF");

    // UISettings — used to detect and track the system theme
    private static readonly UISettings _uiSettings = new();

    // Unit tags — rebuilt per theme so text color is always readable
    private ITag _mmTag = null!;
    private ITag _inTag = null!;

    // Precision tags — no background, theme-independent
    private static readonly ITag RoundedTag = new Tag("~");
    private static readonly ITag ExactTag   = new Tag("=");

    private static bool IsDarkMode()
    {
        // UIColorType.Background is near-black in dark mode, near-white in light mode
        var bg = _uiSettings.GetColorValue(UIColorType.Background);
        return bg.R < 128;
    }

    private void UpdateThemeTags()
    {
        if (IsDarkMode())
        {
            // Dark mode: deep-colored chip backgrounds, light pastel text
            _mmTag = new Tag("mm")
            {
                Background = ColorHelpers.FromRgb(20,  80, 58),   // dark teal
                Foreground = ColorHelpers.FromRgb(179, 242, 221),  // #b3f2dd mint
            };
            _inTag = new Tag("in")
            {
                Background = ColorHelpers.FromRgb(15,  65, 112),  // dark navy
                Foreground = ColorHelpers.FromRgb(145, 213, 255),  // #91D5FF sky blue
            };
        }
        else
        {
            // Light mode: pastel chip backgrounds, dark saturated text
            _mmTag = new Tag("mm")
            {
                Background = ColorHelpers.FromRgb(179, 242, 221),  // #b3f2dd mint
                Foreground = ColorHelpers.FromRgb(13,  51,  38),   // dark forest green
            };
            _inTag = new Tag("in")
            {
                Background = ColorHelpers.FromRgb(145, 213, 255),  // #91D5FF sky blue
                Foreground = ColorHelpers.FromRgb(10,  45,  74),   // dark navy
            };
        }
    }

    public UnitConverterPage()
    {
        Icon = new("\uE8EF");
        Title = "Unit Converter";
        Name = "Convert in ↔ mm";
        PlaceholderText = "Try '1 3/4 in', '3/8 in', '18 gauge', or '3in + 1mm - 0.010in'";

        UpdateThemeTags();
        _uiSettings.ColorValuesChanged += OnColorValuesChanged;
        _items = GetDefaultItems();
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        UpdateThemeTags();
        _items = BuildItems(_currentQuery);
        RaiseItemsChanged();
    }

    // e.g. "18g", "18 ga", "18 gauge", "18gauge"
    [GeneratedRegex(@"^(\d+)\s*ga?(?:uge)?$", RegexOptions.IgnoreCase)]
    private static partial Regex GaugeRegex();

    // e.g. "1 3/4 in", "2 1/2", "5 3/16 inches"
    [GeneratedRegex(@"^(\d+)\s+(\d+)/(\d+)(?:\s*(?:in|inch|inches))?$", RegexOptions.IgnoreCase)]
    private static partial Regex MixedFractionRegex();

    // e.g. "3/8 in", "3/8", "1/2 inch"
    [GeneratedRegex(@"^(\d+)/(\d+)(?:\s*(?:in|inch|inches))?$", RegexOptions.IgnoreCase)]
    private static partial Regex PureFractionRegex();

    // e.g. "25.4 mm", "1 in", "100mm"
    [GeneratedRegex(@"^([\d.]+)\s*(in|inch|inches|mm|millimeters?)$", RegexOptions.IgnoreCase)]
    private static partial Regex DecimalWithUnitRegex();

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _currentQuery = newSearch;
        _items = BuildItems(newSearch);
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => _items;

    private IListItem[] BuildItems(string? query)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query))
            return GetDefaultItems();

        // 1. Gauge lookup — "18g", "18 ga", "18 gauge"
        var gaugeMatch = GaugeRegex().Match(query);
        if (gaugeMatch.Success && int.TryParse(gaugeMatch.Groups[1].Value, out int gauge))
            return BuildGaugeItems(gauge);

        // 2. Mixed fraction + optional unit — "1 3/4 in", "2 1/2"
        var mixedMatch = MixedFractionRegex().Match(query);
        if (mixedMatch.Success
            && double.TryParse(mixedMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double whole)
            && double.TryParse(mixedMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double mixedNum)
            && double.TryParse(mixedMatch.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double mixedDen)
            && mixedDen != 0)
        {
            double inches = whole + mixedNum / mixedDen;
            string label = $"{mixedMatch.Groups[1].Value} {mixedMatch.Groups[2].Value}/{mixedMatch.Groups[3].Value} in";
            return BuildInToMmItems(inches, label);
        }

        // 3. Pure fraction + optional unit — "3/8 in", "3/8"
        var fracMatch = PureFractionRegex().Match(query);
        if (fracMatch.Success
            && double.TryParse(fracMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double fracNum)
            && double.TryParse(fracMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out double fracDen)
            && fracDen != 0)
        {
            double inches = fracNum / fracDen;
            string label = $"{fracMatch.Groups[1].Value}/{fracMatch.Groups[2].Value} in";
            return BuildInToMmItems(inches, label);
        }

        // 4. Decimal with explicit unit — "25.4 mm", "1 in"
        var unitMatch = DecimalWithUnitRegex().Match(query);
        if (unitMatch.Success
            && double.TryParse(unitMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double unitValue))
        {
            string unit = unitMatch.Groups[2].Value.ToLowerInvariant();
            if (unit.StartsWith("in", StringComparison.Ordinal))
                return BuildInToMmItems(unitValue, $"{unitMatch.Groups[1].Value} in");
            else
                return BuildMmToInItems(unitValue, $"{unitMatch.Groups[1].Value} mm");
        }

        // 5. Mixed-unit math expression — "3in + 1mm - 0.010in"
        if (ExpressionParser.TryParse(query, out double resultMm, out string expressionLabel))
            return BuildExpressionItems(resultMm, expressionLabel);

        // 6. Bare number — show both directions
        if (double.TryParse(query, NumberStyles.Float, CultureInfo.InvariantCulture, out double bareValue))
        {
            return [
                ..BuildMmToInItems(bareValue, $"{query} mm"),
                ..BuildInToMmItems(bareValue, $"{query} in"),
            ];
        }

        return [
            new ListItem(new NoOpCommand())
            {
                Title = "Couldn't parse input",
                Subtitle = "Try '25.4 in', '3/8 in', '1 3/4 in', '18 gauge', or '3in + 1mm - 0.010in'"
            }
        ];
    }

    // Title  = the value to copy (prominent in the tile)
    // Subtitle = the input, so the user sees where the result came from
    // Icon   = shared ruler
    // Tags   = unit (colored) + precision (~rounded / =exact)

    private IListItem[] BuildInToMmItems(double inches, string inputLabel)
    {
        double mm = inches * MmPerInch;
        return [
            new ListItem(new CopyTextCommand(RoundedMm(mm)))
            {
                Title    = RoundedMm(mm),
                Subtitle = inputLabel,
                Icon     = RulerIcon,
                Tags     = [_mmTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactMm(mm)))
            {
                Title    = ExactMm(mm),
                Subtitle = inputLabel,
                Icon     = RulerIcon,
                Tags     = [_mmTag, ExactTag],
            },
        ];
    }

    private IListItem[] BuildMmToInItems(double mm, string inputLabel)
    {
        double inches = mm / MmPerInch;
        return [
            new ListItem(new CopyTextCommand(RoundedIn(inches)))
            {
                Title    = RoundedIn(inches),
                Subtitle = inputLabel,
                Icon     = RulerIcon,
                Tags     = [_inTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactIn(inches)))
            {
                Title    = ExactIn(inches),
                Subtitle = inputLabel,
                Icon     = RulerIcon,
                Tags     = [_inTag, ExactTag],
            },
        ];
    }

    private IListItem[] BuildExpressionItems(double resultMm, string expressionLabel)
    {
        double resultIn = resultMm / MmPerInch;
        return [
            new ListItem(new CopyTextCommand(RoundedIn(resultIn)))
            {
                Title    = RoundedIn(resultIn),
                Subtitle = expressionLabel,
                Icon     = RulerIcon,
                Tags     = [_inTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactIn(resultIn)))
            {
                Title    = ExactIn(resultIn),
                Subtitle = expressionLabel,
                Icon     = RulerIcon,
                Tags     = [_inTag, ExactTag],
            },
            new ListItem(new CopyTextCommand(RoundedMm(resultMm)))
            {
                Title    = RoundedMm(resultMm),
                Subtitle = expressionLabel,
                Icon     = RulerIcon,
                Tags     = [_mmTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactMm(resultMm)))
            {
                Title    = ExactMm(resultMm),
                Subtitle = expressionLabel,
                Icon     = RulerIcon,
                Tags     = [_mmTag, ExactTag],
            },
        ];
    }

    private IListItem[] BuildGaugeItems(int gauge)
    {
        if (!GaugeTable.TryGetThicknessInches(gauge, out double thicknessIn))
        {
            return [
                new ListItem(new NoOpCommand())
                {
                    Title    = $"{gauge} gauge is out of range",
                    Subtitle = "Standard steel gauge range is 3–30",
                    Icon     = RulerIcon,
                }
            ];
        }

        double thicknessMm = thicknessIn * MmPerInch;
        string subtitle = $"{gauge} gauge · steel (MSG)";
        return [
            new ListItem(new CopyTextCommand(RoundedIn(thicknessIn)))
            {
                Title    = RoundedIn(thicknessIn),
                Subtitle = subtitle,
                Icon     = RulerIcon,
                Tags     = [_inTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactIn(thicknessIn)))
            {
                Title    = ExactIn(thicknessIn),
                Subtitle = subtitle,
                Icon     = RulerIcon,
                Tags     = [_inTag, ExactTag],
            },
            new ListItem(new CopyTextCommand(RoundedMm(thicknessMm)))
            {
                Title    = RoundedMm(thicknessMm),
                Subtitle = subtitle,
                Icon     = RulerIcon,
                Tags     = [_mmTag, RoundedTag],
            },
            new ListItem(new CopyTextCommand(ExactMm(thicknessMm)))
            {
                Title    = ExactMm(thicknessMm),
                Subtitle = subtitle,
                Icon     = RulerIcon,
                Tags     = [_mmTag, ExactTag],
            },
        ];
    }

    private static IListItem[] GetDefaultItems() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title    = "Enter a value to convert",
            Subtitle = "Examples: '1 in', '25.4 mm', '3/8 in', '1 3/4 in', '18 gauge'",
            Icon     = RulerIcon,
        }
    ];

    // Rounded: F2 for mm (e.g. "25.40 mm"), F3 for in (e.g. "0.984 in")
    private static string RoundedMm(double value) => $"{value:F2} mm";
    private static string RoundedIn(double value) => $"{value:F3} in";

    // Exact: up to 6 significant figures, trailing zeros stripped by G format
    private static string ExactMm(double value) => $"{value:G6} mm";
    private static string ExactIn(double value) => $"{value:G6} in";
}

//internal sealed partial class NoOpCommand : InvokableCommand
//{
//    public NoOpCommand()
//    {
//        Name = "Info";
//    }

//    public override ICommandResult Invoke()
//    {
//        return CommandResult.KeepOpen();
//    }
//}
