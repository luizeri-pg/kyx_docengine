namespace KYX.NotifyHUB.API.Models.DTOs.Notification;

public class NotificationSendRequest
{
    public string? RequisicaoId { get; set; }
    public NotificationConfig Config { get; set; } = new();
    public NotificationDados Dados { get; set; } = new();
}

public class NotificationConfig
{
    public string Canal { get; set; } = string.Empty; // email, sms, whatsapp
    public string? CentroCusto { get; set; }
    public string? IntegracaoId { get; set; }
}

public class NotificationDados
{
    public Remetente? Remetente { get; set; }
    public Destinatario Destinatario { get; set; } = new();
    public Mensagem Mensagem { get; set; } = new();
    public List<Anexo>? Anexos { get; set; }
}

public class Remetente
{
    public string? Email { get; set; }
    public string? Telefone { get; set; }
}

public class Destinatario
{
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Nome { get; set; }
}

public class Mensagem
{
    public string Tipo { get; set; } = "text"; // text, html, template
    public string? Assunto { get; set; }
    public string Corpo { get; set; } = string.Empty;
}

public class Anexo
{
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Base64 { get; set; } = string.Empty;
}

public class NotificationSendResponse
{
    public string Canal { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
}

