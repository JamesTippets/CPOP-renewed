namespace LoiterScan.Engine.Internal;

internal readonly record struct TimeWindow(DateTime Start, DateTime End)
{
    public bool Overlaps(TimeWindow other) => Start <= other.End && End >= other.Start;
    public TimeWindow ExpandBy(TimeSpan margin) => new(Start - margin, End + margin);
}
