using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Models.DTOs.Notification;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Services;

public interface IEmailService
{
    Task<EmailSendResult> SendEmailAsync(NotificationSendRequest request, string? credenciaisJson = null);
    Task<bool> TestConnectionAsync(string credenciaisJson);
    Task<EmailSendResult> SendTestEmailAsync(string credenciaisJson, string emailDestino);
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ThreadId { get; set; }
    public string Canal { get; set; } = "email";
    public string? Destinatario { get; set; }
    public string? Assunto { get; set; }
    public string? Error { get; set; }
}

public class EmailService : IEmailService
{
    private readonly AppSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<AppSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendEmailAsync(NotificationSendRequest request, string? credenciaisJson = null)
    {
        if (_settings.Email.DryRun)
        {
            _logger.LogInformation("[DRY RUN] Email não enviado - modo dry run ativado");
            return new EmailSendResult
            {
                Success = true,
                MessageId = $"dry-run-{Guid.NewGuid()}",
                Canal = "email",
                Destinatario = request.Dados.Destinatario.Email,
                Assunto = request.Dados.Mensagem.Assunto
            };
        }

        // Determina qual método usar baseado nas credenciais
        var credenciais = ParseCredenciais(credenciaisJson);
        
        if (HasSmtpCredentials(credenciais))
        {
            return await SendViaSmtpAsync(request, credenciais);
        }
        else if (HasGmailCredentials(credenciais))
        {
            return await SendViaGmailApiAsync(request, credenciais);
        }
        else
        {
            // Usa configuração padrão
            if (_settings.Email.Provider == "gmail")
            {
                return await SendViaGmailApiAsync(request, null);
            }
            return await SendViaSmtpAsync(request, null);
        }
    }

    private async Task<EmailSendResult> SendViaSmtpAsync(NotificationSendRequest request, Dictionary<string, string>? credenciais)
    {
        var host = credenciais?.GetValueOrDefault("host") ?? credenciais?.GetValueOrDefault("smtpHost") ?? _settings.Smtp.Host;
        var port = int.Parse(credenciais?.GetValueOrDefault("port") ?? credenciais?.GetValueOrDefault("smtpPort") ?? _settings.Smtp.Port.ToString());
        var secure = bool.Parse(credenciais?.GetValueOrDefault("secure") ?? credenciais?.GetValueOrDefault("smtpSecure") ?? _settings.Smtp.Secure.ToString());
        var user = credenciais?.GetValueOrDefault("user") ?? credenciais?.GetValueOrDefault("smtpUser") ?? _settings.Smtp.User;
        var password = credenciais?.GetValueOrDefault("password") ?? credenciais?.GetValueOrDefault("smtpPassword") ?? _settings.Smtp.Password;
        var fromEmail = credenciais?.GetValueOrDefault("fromEmail") ?? credenciais?.GetValueOrDefault("smtpFromEmail") ?? _settings.Smtp.FromEmail;
        var fromName = credenciais?.GetValueOrDefault("fromName") ?? credenciais?.GetValueOrDefault("smtpFromName") ?? _settings.Smtp.FromName;

        if (string.IsNullOrEmpty(fromEmail))
            fromEmail = user;

        try
        {
            _logger.LogInformation("Enviando email via SMTP: {Host}:{Port}", host, port);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(request.Dados.Destinatario.Nome ?? "", request.Dados.Destinatario.Email!));
            message.Subject = request.Dados.Mensagem.Assunto ?? "Notificação";

            var bodyBuilder = new BodyBuilder();
            if (request.Dados.Mensagem.Tipo == "html")
            {
                bodyBuilder.HtmlBody = request.Dados.Mensagem.Corpo;
            }
            else
            {
                bodyBuilder.TextBody = request.Dados.Mensagem.Corpo;
            }

            // Anexos
            if (request.Dados.Anexos != null)
            {
                foreach (var anexo in request.Dados.Anexos)
                {
                    var bytes = Convert.FromBase64String(anexo.Base64);
                    bodyBuilder.Attachments.Add(anexo.Nome, bytes, ContentType.Parse(anexo.Tipo));
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = secure ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(user, password);
            var response = await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email enviado com sucesso via SMTP: {Response}", response);

            return new EmailSendResult
            {
                Success = true,
                MessageId = response,
                Canal = "email",
                Destinatario = request.Dados.Destinatario.Email,
                Assunto = request.Dados.Mensagem.Assunto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email via SMTP");
            return new EmailSendResult
            {
                Success = false,
                Error = ex.Message,
                Canal = "email",
                Destinatario = request.Dados.Destinatario.Email,
                Assunto = request.Dados.Mensagem.Assunto
            };
        }
    }

    private async Task<EmailSendResult> SendViaGmailApiAsync(NotificationSendRequest request, Dictionary<string, string>? credenciais)
    {
        var clientId = credenciais?.GetValueOrDefault("clientId") ?? credenciais?.GetValueOrDefault("gmailClientId") ?? _settings.Gmail.ClientId;
        var clientSecret = credenciais?.GetValueOrDefault("clientSecret") ?? credenciais?.GetValueOrDefault("gmailClientSecret") ?? _settings.Gmail.ClientSecret;
        var refreshToken = credenciais?.GetValueOrDefault("refreshToken") ?? credenciais?.GetValueOrDefault("gmailRefreshToken") ?? _settings.Gmail.RefreshToken;
        var userEmail = credenciais?.GetValueOrDefault("userEmail") ?? credenciais?.GetValueOrDefault("gmailUserEmail") ?? _settings.Gmail.UserEmail;

        try
        {
            _logger.LogInformation("Enviando email via Gmail API: {UserEmail}", userEmail);

            var credential = GoogleCredential.FromAccessToken(await GetAccessTokenAsync(clientId, clientSecret, refreshToken));
            
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "KYX NotifyHUB"
            });

            var remetente = request.Dados.Remetente?.Email ?? userEmail;
            var destinatario = request.Dados.Destinatario.Email;
            var assunto = request.Dados.Mensagem.Assunto ?? "Notificação";
            var corpo = request.Dados.Mensagem.Corpo;
            var contentType = request.Dados.Mensagem.Tipo == "html" ? "text/html" : "text/plain";

            var rawMessage = $"From: {remetente}\r\nTo: {destinatario}\r\nSubject: {assunto}\r\nContent-Type: {contentType}; charset=UTF-8\r\n\r\n{corpo}";
            var encodedMessage = Base64UrlEncode(rawMessage);

            var gmailMessage = new Message { Raw = encodedMessage };
            var response = await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();

            _logger.LogInformation("Email enviado com sucesso via Gmail API: {MessageId}", response.Id);

            return new EmailSendResult
            {
                Success = true,
                MessageId = response.Id,
                ThreadId = response.ThreadId,
                Canal = "email",
                Destinatario = destinatario,
                Assunto = assunto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar email via Gmail API");
            return new EmailSendResult
            {
                Success = false,
                Error = ex.Message,
                Canal = "email",
                Destinatario = request.Dados.Destinatario.Email,
                Assunto = request.Dados.Mensagem.Assunto
            };
        }
    }

    public async Task<bool> TestConnectionAsync(string credenciaisJson)
    {
        // Em modo DryRun, sempre retorna sucesso
        if (_settings.Email.DryRun)
        {
            _logger.LogInformation("[DRY RUN] Teste de conexão simulado - modo dry run ativado");
            return true;
        }

        var credenciais = ParseCredenciais(credenciaisJson);

        // Se não há credenciais na integração, tenta usar as do appsettings.json
        if (credenciais == null || credenciais.Count == 0)
        {
            _logger.LogInformation("Credenciais da integração vazias, usando configuração do appsettings.json");
            
            // Tenta SMTP do appsettings
            if (!string.IsNullOrEmpty(_settings.Smtp.Host) && !string.IsNullOrEmpty(_settings.Smtp.User))
            {
                credenciais = new Dictionary<string, string>
                {
                    ["host"] = _settings.Smtp.Host,
                    ["port"] = _settings.Smtp.Port.ToString(),
                    ["secure"] = _settings.Smtp.Secure.ToString().ToLower(),
                    ["user"] = _settings.Smtp.User,
                    ["password"] = _settings.Smtp.Password,
                    ["fromEmail"] = _settings.Smtp.FromEmail ?? _settings.Smtp.User,
                    ["fromName"] = _settings.Smtp.FromName
                };
            }
            // Tenta Gmail do appsettings
            else if (!string.IsNullOrEmpty(_settings.Gmail.ClientId))
            {
                credenciais = new Dictionary<string, string>
                {
                    ["clientId"] = _settings.Gmail.ClientId,
                    ["clientSecret"] = _settings.Gmail.ClientSecret,
                    ["refreshToken"] = _settings.Gmail.RefreshToken,
                    ["userEmail"] = _settings.Gmail.UserEmail
                };
            }
            else
            {
                _logger.LogWarning("Nenhuma credencial configurada (nem na integração, nem no appsettings.json)");
                return false;
            }
        }

        if (HasSmtpCredentials(credenciais))
        {
            return await TestSmtpConnectionAsync(credenciais!);
        }
        else if (HasGmailCredentials(credenciais))
        {
            return await TestGmailConnectionAsync(credenciais!);
        }

        return false;
    }

    public async Task<EmailSendResult> SendTestEmailAsync(string credenciaisJson, string emailDestino)
    {
        // Em modo DryRun, simula envio bem-sucedido
        if (_settings.Email.DryRun)
        {
            _logger.LogInformation("[DRY RUN] Email de teste simulado para: {Email}", emailDestino);
            return new EmailSendResult
            {
                Success = true,
                MessageId = $"dry-run-test-{Guid.NewGuid()}",
                Canal = "email",
                Destinatario = emailDestino,
                Assunto = "🧪 Teste de Conexão - KYX NotifyHUB (DRY RUN)"
            };
        }

        var testRequest = new NotificationSendRequest
        {
            Config = new NotificationConfig { Canal = "email" },
            Dados = new NotificationDados
            {
                Destinatario = new Destinatario { Email = emailDestino },
                Mensagem = new Mensagem
                {
                    Tipo = "html",
                    Assunto = "🧪 Teste de Conexão - KYX NotifyHUB",
                    Corpo = @"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                            <h2 style='color: #f97316;'>✅ Teste de Conexão Bem-Sucedido!</h2>
                            <p>Este é um email de teste enviado pelo <strong>KYX NotifyHUB</strong>.</p>
                            <p>Se você recebeu este email, significa que a configuração está funcionando corretamente.</p>
                            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                            <p style='color: #666; font-size: 12px;'>
                                <strong>Data/Hora:</strong> " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @"
                            </p>
                        </div>"
                }
            }
        };

        return await SendEmailAsync(testRequest, credenciaisJson);
    }

    private async Task<bool> TestSmtpConnectionAsync(Dictionary<string, string> credenciais)
    {
        var host = credenciais.GetValueOrDefault("host") ?? credenciais.GetValueOrDefault("smtpHost") ?? "";
        var port = int.Parse(credenciais.GetValueOrDefault("port") ?? credenciais.GetValueOrDefault("smtpPort") ?? "587");
        var secure = bool.Parse(credenciais.GetValueOrDefault("secure") ?? credenciais.GetValueOrDefault("smtpSecure") ?? "false");
        var user = credenciais.GetValueOrDefault("user") ?? credenciais.GetValueOrDefault("smtpUser") ?? "";
        var password = credenciais.GetValueOrDefault("password") ?? credenciais.GetValueOrDefault("smtpPassword") ?? "";

        try
        {
            using var client = new SmtpClient();
            var secureOption = secure ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(host, port, secureOption);
            await client.AuthenticateAsync(user, password);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão SMTP");
            return false;
        }
    }

    private async Task<bool> TestGmailConnectionAsync(Dictionary<string, string> credenciais)
    {
        var clientId = credenciais.GetValueOrDefault("clientId") ?? credenciais.GetValueOrDefault("gmailClientId") ?? "";
        var clientSecret = credenciais.GetValueOrDefault("clientSecret") ?? credenciais.GetValueOrDefault("gmailClientSecret") ?? "";
        var refreshToken = credenciais.GetValueOrDefault("refreshToken") ?? credenciais.GetValueOrDefault("gmailRefreshToken") ?? "";

        try
        {
            var accessToken = await GetAccessTokenAsync(clientId, clientSecret, refreshToken);
            var credential = GoogleCredential.FromAccessToken(accessToken);
            
            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "KYX NotifyHUB"
            });

            var profile = await service.Users.GetProfile("me").ExecuteAsync();
            _logger.LogInformation("Gmail API conectado: {Email}", profile.EmailAddress);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão Gmail API");
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, string refreshToken)
    {
        using var client = new HttpClient();
        var response = await client.PostAsync("https://oauth2.googleapis.com/token", 
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }));

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new Exception("Failed to get access token");
    }

    private Dictionary<string, string>? ParseCredenciais(string? credenciaisJson)
    {
        if (string.IsNullOrEmpty(credenciaisJson))
            return null;

        try
        {
            // Tenta deserializar como Dictionary<string, string> primeiro
            var result = JsonSerializer.Deserialize<Dictionary<string, string>>(credenciaisJson);
            if (result != null && result.Count > 0)
                return result;
        }
        catch
        {
            // Se falhar, tenta como JsonDocument para lidar com tipos mistos
        }

        try
        {
            // Lida com JSON que tem valores mistos (números, booleans, strings)
            using var doc = JsonDocument.Parse(credenciaisJson);
            var result = new Dictionary<string, string>();
            
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.GetRawText()
                };
            }
            
            _logger.LogInformation("Credenciais parseadas com sucesso: {Keys}", string.Join(", ", result.Keys));
            return result.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear credenciais JSON: {Json}", credenciaisJson);
            return null;
        }
    }

    private bool HasSmtpCredentials(Dictionary<string, string>? credenciais)
    {
        if (credenciais == null) return false;
        return credenciais.ContainsKey("host") || credenciais.ContainsKey("smtpHost");
    }

    private bool HasGmailCredentials(Dictionary<string, string>? credenciais)
    {
        if (credenciais == null) return false;
        return credenciais.ContainsKey("clientId") || credenciais.ContainsKey("gmailClientId");
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

