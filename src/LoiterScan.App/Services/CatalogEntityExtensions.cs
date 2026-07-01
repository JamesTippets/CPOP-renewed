using LoiterScan.Core.Models;
using LoiterScan.Data.Entities;

namespace LoiterScan.App.Services;

internal static class CatalogEntityExtensions
{
    internal static MeanElements ToMeanElements(this CatalogObjectEntity c) => new(
        MeanMotionRevPerDay: c.MeanMotionRevPerDay,
        Eccentricity:        c.Eccentricity,
        InclinationDeg:      c.InclinationDeg,
        RaanDeg:             c.RaanDeg,
        ArgPerigeeDeg:       c.ArgPerigeeDeg,
        MeanAnomalyDeg:      c.MeanAnomalyDeg,
        BStar:               c.BStar,
        EpochUtc:            c.EpochUtc);
}
