namespace KYX.DocEngine.API.Configuration;

/// <summary>
/// Configuração de autenticação DocEngine ao padrão NotifyHUB.
/// <see cref="Mode"/> define qual implementação usar: PartnerDB (Dapper), StandardDB (EF Core) ou FallbackOnly.
/// </summary>
public class AuthSettings
{
    /// <summary>
    /// Modo de autenticação:
    /// <list type="bullet">
    /// <item><term>PartnerDB</term> - Usa Dapper com SQL nativo para tb_usuario legada (padrão Partner)</item>
    /// <item><term>StandardDB</term> - Usa EF Core com mapeamento padrão (migrações DocEngine)</item>
    /// <item><term>FallbackOnly</term> - Só login em memória (AllowedLogins), sem banco</item>
    /// </list>
    /// </summary>
    public string Mode { get; set; } = "PartnerDB";

    /// <summary>
    /// Se true e Mode=PartnerDB falhar, tenta FallbackAuthService (AllowedLogins).
    /// </summary>
    public bool FallbackEnabled { get; set; }

    /// <summary>Validar contra PostgreSQL (legado, use Mode=PartnerDB).</summary>
    [Obsolete("Use Mode=PartnerDB ou Mode=StandardDB")]
    public bool UseDatabaseForLogin { get; set; } = true;

    /// <summary>
    /// <c>BcryptOnly</c> — só aceita hashes BCrypt.<br/>
    /// <c>BcryptOrMd5Hex</c> — se não for BCrypt, tenta MD5 hex UTF-8.
    /// </summary>
    public string PasswordVerification { get; set; } = "BcryptOrMd5Hex";

    /// <summary>
    /// Se true, tenta <see cref="AllowedLogins"/> quando BD falha.
    /// Mantido para compatibilidade - use <see cref="FallbackEnabled"/>.
    /// </summary>
    [Obsolete("Use FallbackEnabled")]
    public bool UseAllowedLoginsFallback { get; set; }

    /// <summary>Lista de logins em memória para fallback/emergência.</summary>
    public List<AuthLoginPair> AllowedLogins { get; set; } = new();

    /// <summary>Verifica se modo é PartnerDB.</summary>
    public bool IsPartnerDbMode => string.Equals(Mode, "PartnerDB", StringComparison.OrdinalIgnoreCase);

    /// <summary>Verifica se modo é FallbackOnly.</summary>
    public bool IsFallbackOnlyMode => string.Equals(Mode, "FallbackOnly", StringComparison.OrdinalIgnoreCase);

    /// <summary>Verifica se modo é StandardDB.</summary>
    public bool IsStandardDbMode => string.Equals(Mode, "StandardDB", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Par usuário/senha para fallback.</summary>
public class AuthLoginPair
{
    public string Username { get; set; } = string.Empty;
    /// <summary>Hash BCrypt da senha. Gerar com: dotnet run --project tools/HashPassword -- "senha"</summary>
    public string Password { get; set; } = string.Empty;
    /// <summary>Role/Perfil atribuído (default: admin).</summary>
    public string Role { get; set; } = "admin";
}
