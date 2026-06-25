using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using SGPdotNET.Observation;

namespace LoiterScan.Propagation.Sgp4;

/// <summary>
/// SGP4 propagator using SGP.NET (parzivail/SGP.NET, Vallado-based algorithm).
/// IPropagator accepts MeanElements directly — no TLE string crosses the public boundary.
/// Internally a synthetic TLE is composed to satisfy the SGP.NET 1.4.x API; the
/// catalog-number slot is a placeholder and does not influence propagation results.
/// </summary>
public sealed class Sgp4Propagator : IPropagator
{
    public OrbitState Propagate(MeanElements elements, DateTime atUtc)
    {
        var (line1, line2) = FormatSyntheticTle(elements);
        var satellite = new Satellite(line1, line2);
        var eci = satellite.Predict(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc));

        return new OrbitState(
            atUtc,
            eci.Position.X, eci.Position.Y, eci.Position.Z,
            eci.Velocity.X, eci.Velocity.Y, eci.Velocity.Z);
    }

    public bool TryPropagate(MeanElements elements, DateTime atUtc, out OrbitState state)
    {
        try
        {
            state = Propagate(elements, atUtc);
            return true;
        }
        catch
        {
            state = default;
            return false;
        }
    }

    // ── Synthetic TLE formatter ───────────────────────────────────────────────
    //
    // TLE line-1 column map (0-indexed, 69 chars total):
    //  [00]     '1'
    //  [01]     ' '
    //  [02-06]  catalog number      (5 digits, placeholder "00001")
    //  [07]     classification 'U'
    //  [08]     ' '
    //  [09-16]  intl designator     (8 chars: "00000A  ")
    //  [17]     ' '
    //  [18-19]  epoch year          (2 digits)
    //  [20-31]  epoch day           (000.00000000, 12 chars)
    //  [32]     ' '
    //  [33-42]  ndot/2              (S.NNNNNNNN, 10 chars)
    //  [43]     ' '
    //  [44-51]  nddot/6             (decimal-assumed, 8 chars)
    //  [52]     ' '
    //  [53-60]  B*                  (decimal-assumed, 8 chars)
    //  [61]     ' '
    //  [62]     ephemeris type '0'
    //  [63]     ' '
    //  [64-67]  element set number  (4 chars, " 999")
    //  [68]     checksum
    //
    // TLE line-2 column map (0-indexed, 69 chars total):
    //  [00]     '2'
    //  [01]     ' '
    //  [02-06]  catalog number      (5 digits)
    //  [07]     ' '
    //  [08-15]  inclination         (NNN.NNNN, 8 chars)
    //  [16]     ' '
    //  [17-24]  RAAN                (NNN.NNNN, 8 chars)
    //  [25]     ' '
    //  [26-32]  eccentricity        (NNNNNNN, 7 chars, decimal assumed)
    //  [33]     ' '
    //  [34-41]  arg of perigee      (NNN.NNNN, 8 chars)
    //  [42]     ' '
    //  [43-50]  mean anomaly        (NNN.NNNN, 8 chars)
    //  [51]     ' '
    //  [52-62]  mean motion         (NN.NNNNNNNN, 11 chars)
    //  [63-67]  rev at epoch        (5 digits, placeholder "00000")
    //  [68]     checksum

    private static (string line1, string line2) FormatSyntheticTle(MeanElements e)
    {
        const string satNum = "00001";

        int    y2  = e.EpochUtc.Year % 100;
        double doy = e.EpochUtc.DayOfYear
                   + e.EpochUtc.TimeOfDay.TotalSeconds / 86400.0;

        string ndot  = FormatNdot(0.0);
        string nddot = FormatDecimalAssumed(0.0);
        string bstar = FormatDecimalAssumed(e.BStar);

        // 68 chars; checksum appended below
        string l1 =
            $"1 {satNum}U 00000A   {y2:D2}{doy:000.00000000} {ndot} {nddot} {bstar} 0  999";

        long eccInt = (long)Math.Round(e.Eccentricity * 1e7);

        string l2 =
            $"2 {satNum} {e.InclinationDeg,8:F4} {e.RaanDeg,8:F4} {eccInt:D7}" +
            $" {e.ArgPerigeeDeg,8:F4} {e.MeanAnomalyDeg,8:F4} {e.MeanMotionRevPerDay,11:F8}00000";

        return (l1 + TleChecksum(l1), l2 + TleChecksum(l2));
    }

    /// <summary>Formats ndot/2 as S.NNNNNNNN (10 chars).</summary>
    private static string FormatNdot(double value)
    {
        char sign = value >= 0 ? ' ' : '-';
        // "0.NNNNNNNN" → drop leading "0" → ".NNNNNNNN"
        string frac = $"{Math.Abs(value):0.00000000}".Substring(1);
        return $"{sign}{frac}";
    }

    /// <summary>
    /// Formats a value in TLE decimal-point-assumed notation (8 chars: SXXXXXEY).
    /// Represents 0.XXXXX × 10^Y where S is the overall sign.
    /// </summary>
    private static string FormatDecimalAssumed(double value)
    {
        if (value == 0.0 || Math.Abs(value) < 1e-99) return " 00000-0";

        char   sign   = value >= 0 ? ' ' : '-';
        double absVal = Math.Abs(value);
        int    exp    = (int)Math.Floor(Math.Log10(absVal)) + 1;
        int    mant   = (int)Math.Round(absVal * Math.Pow(10, 5 - exp));

        // Guard: rounding may push mantissa to 100000
        if (mant >= 100000) { exp++; mant = 10000; }
        if (mant <= 0)      return $"{sign}00000-0";

        string expStr = exp >= 0 ? $"+{exp}" : $"{exp}";
        return $"{sign}{mant:D5}{expStr}";
    }

    /// <summary>TLE checksum: sum of digit chars + 1 for each '-', mod 10.</summary>
    private static char TleChecksum(string line)
    {
        int sum = 0;
        foreach (char c in line)
        {
            if (c is >= '0' and <= '9') sum += c - '0';
            else if (c == '-') sum += 1;
        }
        return (char)('0' + sum % 10);
    }
}
