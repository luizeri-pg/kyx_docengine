using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KYX.DocEngine.API.Configuration;

/// <summary>
/// Lê as colunas reais de <c>tb_usuario</c> no PostgreSQL e, se corresponderem ao modelo Notify/KYX
/// (<c>str_login</c>, <c>id_usuario</c>, …), carrega <c>appsettings.LegacyTbUsuario.json</c> por cima do
/// <c>appsettings.json</c> — sem variáveis de ambiente manuais no Swarm.
/// </summary>
public static class LegacyTbUsuarioSchemaProbe
{
    private const string LegacySettingsFile = "appsettings.LegacyTbUsuario.json";

    /// <summary>
    /// Se a BD estiver acessível e <c>tb_usuario</c> tiver colunas legadas, adiciona o JSON de schema.
    /// Falhas de rede/BD são ignoradas (mantém-se o que veio dos ficheiros já carregados).
    /// </summary>
    public static void MergeIfDatabaseMatchesLegacyModel(ConfigurationManager configuration)
    {
        var cs = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            return;

        try
        {
            using var conn = new NpgsqlConnection(cs);
            conn.Open();
            if (!TbUsuarioLooksLikeLegacyKyx(conn))
                return;

            var path = Path.Combine(AppContext.BaseDirectory, LegacySettingsFile);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine(
                    $"AVISO: tb_usuario legada detetada mas {LegacySettingsFile} não existe no output da aplicação.");
                return;
            }

            configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
        }
        catch (Exception ex)
        {
            // Arranque sem BD (ou timeout): não bloquear — appsettings.Development / base aplicam-se.
            Console.Error.WriteLine(
                $"[SchemaProbe] Não foi possível inspecionar tb_usuario ({ex.GetType().Name}: {ex.Message}). A usar schema só de ficheiros.");
        }
    }

    /// <summary>Legado típico: login em coluna dedicada e PK inteira.</summary>
    private static bool TbUsuarioLooksLikeLegacyKyx(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'tb_usuario'
            """,
            conn);

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                cols.Add(r.GetString(0));
        }

        if (cols.Count == 0)
            return false;

        if (cols.Contains("str_login"))
            return true;

        // Migrações DocEngine no repo: id + nome, sem str_login
        if (cols.Contains("id") && cols.Contains("nome") && !cols.Contains("str_login"))
            return false;

        return false;
    }
}
