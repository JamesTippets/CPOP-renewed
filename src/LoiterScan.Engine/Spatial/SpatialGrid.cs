namespace LoiterScan.Engine.Spatial;

/// <summary>
/// Bucket-based 3D spatial grid for fast radius queries.
/// Cell size = query threshold, so any two objects within threshold are at most 1 cell apart
/// in each dimension, and querying ±1 (27 cells) is sufficient with no false negatives.
/// </summary>
internal sealed class SpatialGrid
{
    private readonly double _cellSize;
    private readonly Dictionary<(int Cx, int Cy, int Cz), List<int>> _cells = new();

    public SpatialGrid(double cellSize) => _cellSize = cellSize;

    public void Clear() => _cells.Clear();

    public void Add(int index, double x, double y, double z)
    {
        var key = CellOf(x, y, z);
        if (!_cells.TryGetValue(key, out var list))
            _cells[key] = list = [];
        list.Add(index);
    }

    /// <summary>Yields indices of objects within <paramref name="radius"/> of the given point.
    /// May include the queried object itself — callers must filter j == queryIndex.</summary>
    public IEnumerable<int> QueryNeighbours(double x, double y, double z, double radius)
    {
        int steps = (int)Math.Ceiling(radius / _cellSize);
        var (cx, cy, cz) = CellOf(x, y, z);
        for (int ix = cx - steps; ix <= cx + steps; ix++)
        for (int iy = cy - steps; iy <= cy + steps; iy++)
        for (int iz = cz - steps; iz <= cz + steps; iz++)
        {
            if (_cells.TryGetValue((ix, iy, iz), out var list))
                foreach (var idx in list) yield return idx;
        }
    }

    private (int, int, int) CellOf(double x, double y, double z) =>
        ((int)Math.Floor(x / _cellSize),
         (int)Math.Floor(y / _cellSize),
         (int)Math.Floor(z / _cellSize));
}
