using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace tui.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets("e06dae00-410e-4d69-b3fe-9451234a4103")
            .AddEnvironmentVariables().Build();
        var connection = $"Server={configuration["Db:host"]};Port={configuration["Db:port"]};Database={configuration["Db:name"]};User={configuration["Db:user"]};Password={configuration["Db:password"]};";
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connection, new MariaDbServerVersion(new Version(11, 0, 0))).Options);
    }
}
