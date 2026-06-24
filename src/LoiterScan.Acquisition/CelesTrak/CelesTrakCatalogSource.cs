using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>v1 source: CelesTrak GP data (OMM/JSON) joined with SATCAT and GROUP membership.
/// Derived fields (apogee/perigee/regime, epoch age) are computed on ingest.</summary>
public sealed class CelesTrakCatalogSource : ICatalogSource
{
    public Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default)
        => throw new NotImplementedException("TODO: fetch GP (OMM) + SATCAT from CelesTrak.");
}
