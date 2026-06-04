using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AIM.Storage;

public sealed class AimDbContextFactory : IDesignTimeDbContextFactory<AimDbContext>
{
    public AimDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "aim-design-time.db");
        var options = new DbContextOptionsBuilder<AimDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new AimDbContext(options);
    }
}
