using KYX.DocEngine.API.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Data;

/// <summary>
/// Permite que <c>dotnet ef</c> use a mesma cadeia de configuração que o app (inclui appsettings.Local.json).
/// </summary>
public class DocEngineDbContextFactory : IDesignTimeDbContextFactory<DocEngineDbContext>
{
    public DocEngineDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

        var optionsBuilder = new DbContextOptionsBuilder<DocEngineDbContext>();
        optionsBuilder.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly("KYX.DocEngine.API"))
            .UseSnakeCaseNamingConvention();

        var schema = config.GetSection("Schema").Get<SchemaTableOptions>() ?? new SchemaTableOptions();
        return new DocEngineDbContext(optionsBuilder.Options, Options.Create(schema));
    }
}
