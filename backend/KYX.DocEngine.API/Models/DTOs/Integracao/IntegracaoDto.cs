namespace KYX.NotifyHUB.API.Models.DTOs.Integracao;

public class IntegracaoDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Canal { get; set; } = string.Empty;
    public string Provedor { get; set; } = string.Empty;
    public string? UrlBase { get; set; }
    public string? Credenciais { get; set; }
    public bool Ativo { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

public class CreateIntegracaoRequest
{
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Canal { get; set; } = string.Empty; // email, sms, whatsapp
    public string Provedor { get; set; } = string.Empty;
    public string? UrlBase { get; set; }
    public string? Credenciais { get; set; }
    public bool Ativo { get; set; } = true;
}

public class UpdateIntegracaoRequest
{
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public string? Tipo { get; set; }
    public string? Canal { get; set; }
    public string? Provedor { get; set; }
    public string? UrlBase { get; set; }
    public string? Credenciais { get; set; }
    public bool? Ativo { get; set; }
}

public class TestIntegracaoRequest
{
    public string? EmailTeste { get; set; }
}

public class TestIntegracaoResponse
{
    public string IntegracaoId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? TempoResposta { get; set; }
    public bool EmailTesteEnviado { get; set; }
    public string? EmailDestino { get; set; }
    public string? MessageId { get; set; }
    public bool Mock { get; set; }
}

