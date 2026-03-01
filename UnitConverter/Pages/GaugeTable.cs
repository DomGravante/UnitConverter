namespace UnitConverter.Pages;

/// <summary>
/// Manufacturer's Standard Gauge (MSG) thickness values for steel sheet, in inches.
/// Range: 3–30 gauge.
/// </summary>
internal static class GaugeTable
{
    internal static bool TryGetThicknessInches(int gauge, out double inches)
    {
        inches = gauge switch
        {
            3  => 0.2391,
            4  => 0.2242,
            5  => 0.2092,
            6  => 0.1943,
            7  => 0.1793,
            8  => 0.1644,
            9  => 0.1495,
            10 => 0.1345,
            11 => 0.1196,
            12 => 0.1046,
            13 => 0.0897,
            14 => 0.0747,
            15 => 0.0673,
            16 => 0.0598,
            17 => 0.0538,
            18 => 0.0478,
            19 => 0.0418,
            20 => 0.0359,
            21 => 0.0329,
            22 => 0.0299,
            23 => 0.0269,
            24 => 0.0239,
            25 => 0.0209,
            26 => 0.0179,
            27 => 0.0164,
            28 => 0.0149,
            29 => 0.0135,
            30 => 0.0120,
            _  => -1,
        };
        return inches >= 0;
    }
}
