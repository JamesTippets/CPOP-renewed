using System.Net;
using LoiterScan.Acquisition.CelesTrak;
using LoiterScan.Core.Models;

namespace LoiterScan.Acquisition.SpaceTrack;

/// <summary>
/// Fetches catalog data from Space-Track.org using cookie-based authentication.
/// Reuses the same OMM JSON parser as the CelesTrak source; the GP response format is identical.
/// Credentials are passed per-call so they can change without reconstructing the service.
/// </summary>
public sealed class SpaceTrackCatalogSource
{
    private const string BaseUrl   = "https://www.space-track.org";
    private const string LoginUrl  = BaseUrl + "/ajaxauth/login";
    private const string GpUrl     = BaseUrl + "/basicspacedata/query/class/gp/EPOCH/%3Enow-30/orderby/NORAD_CAT_ID/format/json/";
    private const string SatcatUrl = BaseUrl + "/basicspacedata/query/class/satcat/CURRENT/Y/format/json/";

    public async Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(
        string username, string password, CancellationToken ct = default)
    {
        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookies };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };

        var loginResp = await http.PostAsync(LoginUrl,
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("identity", username),
                new KeyValuePair<string, string>("password", password),
            ]), ct);
        loginResp.EnsureSuccessStatusCode();

        // Fetch GP and SATCAT concurrently; SATCAT failure is non-fatal
        var gpTask     = http.GetStringAsync(GpUrl, ct);
        var satcatTask = TryFetchSatcatAsync(http, ct);

        string  gpJson = await gpTask;
        string? satcat = await satcatTask;
        return CelesTrakCatalogSource.BuildCatalog(gpJson, satcat);
    }

    private static async Task<string?> TryFetchSatcatAsync(HttpClient http, CancellationToken ct)
    {
        try { return await http.GetStringAsync(SatcatUrl, ct); }
        catch (HttpRequestException) { return null; }
    }
}
