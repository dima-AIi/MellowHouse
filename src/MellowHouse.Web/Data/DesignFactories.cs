using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace MellowHouse.Web.Data;
public sealed class SqliteFactory : IDesignTimeDbContextFactory<SqliteShopDbContext>
{
    public SqliteShopDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqliteShopDbContext>().UseSqlite("Data Source=App_Data/mellowhouse.db").Options);
}
public sealed class PostgresFactory : IDesignTimeDbContextFactory<PostgresShopDbContext>
{
    public PostgresShopDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PostgresShopDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? "Host=localhost;Database=mellowhouse;Username=mellowhouse").Options);
}
