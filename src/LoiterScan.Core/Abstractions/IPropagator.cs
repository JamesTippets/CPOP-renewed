using LoiterScan.Core.Models;

namespace LoiterScan.Core.Abstractions;

/// <summary>
/// Pluggable propagator. Accepts mean elements directly. v1: Vallado reference SGP4.
/// Future: native SGP4-XP interop — a drop-in, since the engine depends only on this interface.
/// </summary>
public interface IPropagator
{
    OrbitState Propagate(MeanElements elements, DateTime atUtc);
}
