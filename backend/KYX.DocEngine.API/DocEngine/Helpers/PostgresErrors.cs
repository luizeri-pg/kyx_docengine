using Npgsql;

namespace KYX.DocEngine.API.Helpers;

internal static class PostgresErrors
{
    /// <summary>42P01 — relação (tabela) não existe.</summary>
    public static bool IsUndefinedTable(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
        {
            if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.UndefinedTable)
                return true;
        }

        return false;
    }
}
