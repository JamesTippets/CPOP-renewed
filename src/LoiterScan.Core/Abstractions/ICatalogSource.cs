using LoiterScan.Core.Models;

namespace LoiterScan.Core.Abstractions;

/// <summary>Pluggable catalog source. v1: CelesTrak (GP/OMM + SATCAT). Future: Space-Track.</summary>
public interface ICatalogSource
{
    Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default);
}
