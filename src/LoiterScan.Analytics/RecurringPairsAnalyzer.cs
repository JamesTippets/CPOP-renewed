namespace LoiterScan.Analytics;

/// <summary>Groups loitering events by canonical pair key across runs: recurrence count,
/// first/last seen, min range, and the min-range trend (closing vs opening).</summary>
public sealed class RecurringPairsAnalyzer
{
    // TODO: GROUP BY pair_key over runs; key on close-approach time to separate
    //       reconfirmation of one event from genuinely repeated episodes (spec section 8.1).
}
