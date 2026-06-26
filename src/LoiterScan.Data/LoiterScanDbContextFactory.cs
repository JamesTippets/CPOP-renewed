using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LoiterScan.Data;

/// <summary>
/// Required by `dotnet ef migrations add` at design time.
/// Points at a writable temp path so migrations can be generated without a running app.
/// </summary>
public sealed class LoiterScanDbContextFactory : IDesignTimeDbContextFactory<LoiterScanDbContext>
{
    public LoiterScanDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<LoiterScanDbContext>()
            .UseSqlite("Data Source=loiterscan-design.db")
            .Options;
        return new LoiterScanDbContext(opts);
    }
}
