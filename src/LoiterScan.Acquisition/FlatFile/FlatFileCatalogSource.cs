using System.Globalization;
using LoiterScan.Acquisition.CelesTrak;
using LoiterScan.Acquisition.CelesTrak.Dto;
using LoiterScan.Acquisition.SpaceTrack;
using LoiterScan.Core.Models;

namespace LoiterScan.Acquisition.FlatFile;

/// <summary>
/// Reads a standard 3-line TLE flat file (name + two TLE lines per object) and produces
/// catalog objects using the same derivation pipeline as the live sources.
/// Fetches the CelesTrak SATCAT concurrently to populate owner, object-type, and debris flag
/// needed for pre-filtering — non-fatal if the SATCAT endpoint is unavailable.
/// Also handles 2-line TLE files (no name line) by using the NORAD ID as the name.
/// </summary>
public sealed class FlatFileCatalogSource(HttpClient http)
{
    private const string SatcatUrl = "https://www.celestrak.org/pub/satcat.json";

    /// <param name="spaceTrackUser">Optional Space-Track username; used as SATCAT fallback when CelesTrak is unavailable.</param>
    /// <param name="spaceTrackPass">Optional Space-Track password paired with <paramref name="spaceTrackUser"/>.</param>
    public async Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(
        string filePath,
        string? spaceTrackUser = null,
        string? spaceTrackPass = null,
        CancellationToken ct = default)
    {
        // Fetch CelesTrak SATCAT concurrently with file I/O; failure is non-fatal
        var satcatTask = TryFetchCelesTrakSatcatAsync(ct);
        var lines      = await File.ReadAllLinesAsync(filePath, ct);
        var satcatJson = await satcatTask;

        // If CelesTrak SATCAT is unavailable, fall back to Space-Track SATCAT
        if (satcatJson is null && spaceTrackUser is not null && spaceTrackPass is not null)
            satcatJson = await SpaceTrackCatalogSource.FetchSatcatWithCredentialsAsync(spaceTrackUser, spaceTrackPass, ct);

        IReadOnlyDictionary<long, SatcatRecord> satcatById = satcatJson is not null
            ? CelesTrakSatcatParser.Parse(satcatJson)
            : new Dictionary<long, SatcatRecord>();

        return Parse(lines, satcatById);
    }

    private async Task<string?> TryFetchCelesTrakSatcatAsync(CancellationToken ct)
    {
        try   { return await http.GetStringAsync(SatcatUrl, ct); }
        catch (Exception) { return null; }
    }

    private static IReadOnlyList<CatalogObject> Parse(
        string[] lines, IReadOnlyDictionary<long, SatcatRecord> satcatById)
    {
        var result = new List<CatalogObject>();
        var asOf = DateTime.UtcNow;
        int i = 0;

        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;
            if (i >= lines.Length) break;

            string first = lines[i];

            // 2-line TLE format (no name line)
            if (first.Length >= 2 && first[0] == '1' && first[1] == ' ')
            {
                if (i + 1 >= lines.Length) break;
                string l1 = first.Trim();
                string l2 = lines[i + 1].Trim();
                i += 2;
                if (!l2.StartsWith("2 ")) continue;
                try
                {
                    string norad = l1.Substring(2, 5).Trim();
                    var omm = ParseTlePair("SAT " + norad, l1, l2);
                    satcatById.TryGetValue(omm.NoradCatId, out var satcat);
                    result.Add(CatalogObjectMapper.Map(omm, satcat, asOf));
                }
                catch { }
            }
            else
            {
                // 3-line TLE format (name + two TLE lines)
                if (i + 2 >= lines.Length) break;
                string name = first.Trim();
                string l1   = lines[i + 1].Trim();
                string l2   = lines[i + 2].Trim();
                i += 3;
                if (!l1.StartsWith("1 ") || !l2.StartsWith("2 ")) continue;
                try
                {
                    var omm = ParseTlePair(name, l1, l2);
                    satcatById.TryGetValue(omm.NoradCatId, out var satcat);
                    result.Add(CatalogObjectMapper.Map(omm, satcat, asOf));
                }
                catch { }
            }
        }

        return result;
    }

    private static OmmRecord ParseTlePair(string name, string line1, string line2)
    {
        // TLE Line 1 fields (0-indexed substrings)
        long   noradId    = long.Parse(line1.Substring(2, 5).Trim(),  CultureInfo.InvariantCulture);
        string intlDesig  = line1.Substring(9, 8).Trim();
        string epochStr   = line1.Substring(18, 14).Trim();
        double ndot       = double.Parse(line1.Substring(33, 10).Trim(), CultureInfo.InvariantCulture);
        double nddot      = ParseTleFloat(line1.Substring(44, 8).Trim());
        double bstar      = ParseTleFloat(line1.Substring(53, 8).Trim());
        int    ephType    = int.Parse(line1.Substring(62, 1).Trim(),  CultureInfo.InvariantCulture);
        int    elSetNo    = int.Parse(line1.Substring(64, 4).Trim(),  CultureInfo.InvariantCulture);

        // TLE Line 2 fields (0-indexed substrings)
        double incl        = double.Parse(line2.Substring(8,  8).Trim(),  CultureInfo.InvariantCulture);
        double raan        = double.Parse(line2.Substring(17, 8).Trim(),  CultureInfo.InvariantCulture);
        double ecc         = double.Parse("0." + line2.Substring(26, 7).Trim(), CultureInfo.InvariantCulture);
        double argPerigee  = double.Parse(line2.Substring(34, 8).Trim(),  CultureInfo.InvariantCulture);
        double meanAnomaly = double.Parse(line2.Substring(43, 8).Trim(),  CultureInfo.InvariantCulture);
        double meanMotion  = double.Parse(line2.Substring(52, 11).Trim(), CultureInfo.InvariantCulture);
        int    revAtEpoch  = int.Parse(line2.Substring(63, 5).Trim(),  CultureInfo.InvariantCulture);

        return new OmmRecord
        {
            ObjectName      = name,
            NoradCatId      = noradId,
            IntlDesignator  = intlDesig,
            Epoch           = TleEpochToIso8601(epochStr),
            MeanMotion      = meanMotion,
            Eccentricity    = ecc,
            Inclination     = incl,
            RaOfAscNode     = raan,
            ArgOfPericenter = argPerigee,
            MeanAnomaly     = meanAnomaly,
            Bstar           = bstar,
            MeanMotionDot   = ndot,
            MeanMotionDdot  = nddot,
            EphemerisType   = ephType,
            ElementSetNo    = elSetNo,
            RevAtEpoch      = revAtEpoch,
        };
    }

    // Converts TLE epoch string (YYDDD.FFFFFFFF) to ISO 8601 UTC.
    // Years 57–99 are mapped to 1957–1999; 00–56 to 2000–2056.
    private static string TleEpochToIso8601(string epochStr)
    {
        int    year2d    = int.Parse(epochStr.Substring(0, 2), CultureInfo.InvariantCulture);
        double dayFrac   = double.Parse(epochStr.Substring(2),  CultureInfo.InvariantCulture);
        int    fullYear  = year2d >= 57 ? 1900 + year2d : 2000 + year2d;
        int    dayOfYear = (int)dayFrac;
        var    dt        = new DateTime(fullYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                               .AddDays(dayOfYear - 1)
                           + TimeSpan.FromDays(dayFrac - dayOfYear);
        return dt.ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
    }

    // Parses TLE packed decimal: [±]NNNNN[±]E  →  ±0.NNNNN × 10^±E
    // Used for BSTAR and the second derivative of mean motion.
    private static double ParseTleFloat(string s)
    {
        s = s.Trim();
        if (string.IsNullOrEmpty(s)) return 0.0;

        int mantissaSign = 1;
        int pos          = 0;
        if      (s[0] == '-') { mantissaSign = -1; pos = 1; }
        else if (s[0] == '+') { pos = 1; }

        int expIdx = -1;
        for (int j = s.Length - 1; j > pos; j--)
        {
            if (s[j] == '+' || s[j] == '-') { expIdx = j; break; }
        }
        if (expIdx < 0) return 0.0;

        string mantissaDigits = s.Substring(pos, expIdx - pos);
        int    expSign        = s[expIdx] == '-' ? -1 : 1;
        int    exp            = int.Parse(s.Substring(expIdx + 1), CultureInfo.InvariantCulture);
        double mantissa       = double.Parse("0." + mantissaDigits, CultureInfo.InvariantCulture);
        return mantissaSign * mantissa * Math.Pow(10.0, expSign * exp);
    }
}
