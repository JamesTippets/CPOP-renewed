using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;

namespace LoiterScan.Propagation.Sgp4;

/// <summary>v1 propagator: Vallado public-domain reference SGP4 (to be integrated).</summary>
public sealed class Sgp4Propagator : IPropagator
{
    public OrbitState Propagate(MeanElements elements, DateTime atUtc)
        => throw new NotImplementedException("TODO: integrate Vallado reference SGP4.");
}
