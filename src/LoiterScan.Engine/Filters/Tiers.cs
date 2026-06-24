namespace LoiterScan.Engine.Filters;

// Tier stubs — see docs/loiter-detection-spec.md, section 5.
internal static class PreFilter { /* exclusions + apogee/perigee gating + regime scope */ }
internal static class CoarseFilter { /* 15 min / 50 km / per-timestep spatial index */ }
internal static class FineFilter { /* 5 min / 25 km / windowed +/-30 min */ }
internal static class LoiteringDetector { /* 1 min / 5 km / contiguous >=1 h, 5 min excursion */ }
