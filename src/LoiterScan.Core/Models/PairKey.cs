namespace LoiterScan.Core.Models;

/// <summary>Canonical unordered pair of NORAD catalog ids (low, high) so A-B and B-A collapse.</summary>
public readonly record struct PairKey
{
    public long Low { get; }
    public long High { get; }

    public PairKey(long a, long b) => (Low, High) = a <= b ? (a, b) : (b, a);

    public override string ToString() => $"{Low}-{High}";
}
