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
        {
            Console.WriteLine("[SchemaProbe] Connection string não definida — schema só de ficheiros.");
            return;
        }

        Console.WriteLine("[SchemaProbe] A ligar à BD para inspecionar tb_usuario...");

        try
        {
            using var conn = new NpgsqlConnection(cs);
            conn.Open();
            Console.WriteLine("[SchemaProbe] Ligação aberta. A ler colunas de tb_usuario...");

            if (!TbUsuarioLooksLikeLegacyKyx(conn))
            {
                Console.WriteLine("[SchemaProbe] tb_usuario não parece ser modelo KYX legado (str_login ausente).");
                return;
            }

            Console.WriteLine("[SchemaProbe] tb_usuario legada detetada (str_login presente).");

            var path = Path.Combine(AppContext.BaseDirectory, LegacySettingsFile);
            Console.WriteLine($"[SchemaProbe] A procurar ficheiro: {path}");

            if (!File.Exists(path))
            {
                Console.Error.WriteLine(
                    $"[SchemaProbe] AVISO: tb_usuario legada detetada mas {LegacySettingsFile} não existe no output da aplicação.");
                return;
            }

            Console.WriteLine($"[SchemaProbe] A aplicar {LegacySettingsFile}...");
            configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
            Console.WriteLine("[SchemaProbe] Schema legado aplicado com sucesso.");
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
