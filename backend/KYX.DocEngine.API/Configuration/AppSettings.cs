namespace KYX.NotifyHUB.API.Configuration;

public class AppSettings
{
    public JwtSettings Jwt { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public GmailSettings Gmail { get; set; } = new();
    public SmtpSettings Smtp { get; set; } = new();
    public bool UseMocks { get; set; } = true;
}

public class JwtSettings
{
    public string SecretKey { get; set; } = "change-me-in-production-minimum-32-characters";
    public string Issuer { get; set; } = "PartnerOneEstreira";
    public string Audience { get; set; } = "PartnerOneEstreira";
    public int ExpirationMinutes { get; set; } = 30;
}

public class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public class EmailSettings
{
    public string Provider { get; set; } = "smtp"; // smtp ou gmail
    public bool DryRun { get; set; } = false;
    public bool ShowPayload { get; set; } = true;
}

public class GmailSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool Secure { get; set; } = false;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "KYX NotifyHUB";
}

