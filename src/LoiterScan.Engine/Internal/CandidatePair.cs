using LoiterScan.Core.Models;

namespace LoiterScan.Engine.Internal;

internal sealed record CandidatePair(
    CatalogObject A,
    CatalogObject B,
    IReadOnlyList<TimeWindow> Windows);
