namespace KYX.DocEngine.API.Configuration;

/// <summary>
/// Autenticação: <see cref="UseDatabaseForLogin"/> usa <c>tb_usuario</c> (ver <see cref="PasswordVerification"/>).
/// <see cref="AllowedLogins"/> só é usado se <see cref="UseAllowedLoginsFallback"/> for true (ex.: dev sem BD).
/// </summary>
public class AuthSettings
{
    /// <summary>Validar contra PostgreSQL (<c>tb_usuario</c>).</summary>
    public bool UseDatabaseForLogin { get; set; } = true;

    /// <summary>
    /// <c>BcryptOnly</c> — só aceita hashes BCrypt (<c>$2a$</c> / <c>$2b$</c>).<br/>
    /// <c>BcryptOrMd5Hex</c> — se o valor na BD não for BCrypt, tenta MD5 da password em UTF-8 comparado com hex de 32 caracteres (bases legadas).
    /// </summary>
    public string PasswordVerification { get; set; } = "BcryptOrMd5Hex";

    /// <summary>
    /// Se false (recomendado em produção), **não** há login por lista em configuração — só a BD.
    /// Se true, tenta <see cref="AllowedLogins"/> quando o utilizador não existe na BD ou em erro de conexão.
    /// </summary>
    public bool UseAllowedLoginsFallback { get; set; }

    /// <summary>Contingência opcional (apenas com <see cref="UseAllowedLoginsFallback"/> = true).</summary>
    public List<AuthLoginPair> AllowedLogins { get; set; } = new();
}

public class AuthLoginPair
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
