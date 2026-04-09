using Npgsql;

namespace KYX.DocEngine.API.Helpers;

public static class ConnectionStringHelper
{
    // TEMPORARIO para teste em DEV sem depender da Library.
    // Reverter apos validar o ambiente.
    private const string TemporaryFixedDefaultConnection =
        "Host=172.19.61.24;Port=5442;Database=doc_engine_dev;Username=doc_engine;Password=s73YhMCG";

    public static string ResolveDefaultConnection(IConfiguration configuration, ILogger? logger = null)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (IsValidConnectionString(configured))
            return configured!;

        logger?.LogWarning(
            "ConnectionStrings:DefaultConnection invalida/ausente. Usando conexao fixa temporaria para teste.");
        return TemporaryFixedDefaultConnection;
    }

    private static bool IsValidConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
