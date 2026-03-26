namespace KYX.DocEngine.API.Configuration;

public class JwtSettings
{
    public string SecretKey { get; set; } = "change-me-in-production-minimum-32-characters";
    public string Issuer { get; set; } = "KYX.DocEngine";
    public string Audience { get; set; } = "KYX.DocEngine";
    public int ExpirationMinutes { get; set; } = 30;
}
