using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Data;
using KYX.NotifyHUB.API.Models.DTOs;
using KYX.NotifyHUB.API.Models.DTOs.Integracao;
using KYX.NotifyHUB.API.Models.Entities;
using KYX.NotifyHUB.API.Services;
using KYX.NotifyHUB.API.Stores;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Controllers;

[ApiController]
[Route("integracoes")]
[Authorize]
public class IntegracoesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly MockStore _mockStore;
    private readonly AppSettings _settings;
    private readonly ILogger<IntegracoesController> _logger;

    public IntegracoesController(
        AppDbContext context,
        IEmailService emailService,
        MockStore mockStore,
        IOptions<AppSettings> settings,
        ILogger<IntegracoesController> logger)
    {
        _context = context;
        _emailService = emailService;
        _mockStore = mockStore;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lista todas as integrações
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<IntegracaoDto>>>> GetAll(
        [FromQuery] string? canal,
        [FromQuery] bool? ativo,
        [FromQuery] bool credentials = false)
    {
        try
        {
            _logger.LogInformation("[INTEGRACOES] Listando integrações - Mock: {UseMocks}", _settings.UseMocks);

            List<IntegracaoDto> resultado;

            if (_settings.UseMocks)
            {
                var integracoes = _mockStore.ListIntegracoes(canal, ativo);
                resultado = integracoes.Select(i => MapToDto(i, credentials)).ToList();
                _logger.LogInformation("[INTEGRACOES] Integrações encontradas (mock): {Count}", resultado.Count);
            }
            else
            {
                var query = _context.Integracoes.AsQueryable();

                if (!string.IsNullOrEmpty(canal))
                    query = query.Where(i => i.Canal == canal);
                if (ativo.HasValue)
                    query = query.Where(i => i.Ativo == ativo.Value);

                var integracoes = await query
                    .OrderByDescending(i => i.CriadoEm)
                    .ToListAsync();

                resultado = integracoes.Select(i => MapToDto(i, credentials)).ToList();
            }

            _logger.LogInformation("[INTEGRACOES] Retornando {Count} integrações", resultado.Count);
            return Ok(ApiResponse<List<IntegracaoDto>>.Success(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INTEGRACOES] Erro ao listar integrações");
            return StatusCode(500, ApiResponse<List<IntegracaoDto>>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Busca uma integração por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<IntegracaoDto>>> GetById(string id, [FromQuery] bool credentials = false)
    {
        try
        {
            Integracao? integracao;

            if (_settings.UseMocks)
            {
                integracao = _mockStore.GetIntegracao(id);
            }
            else
            {
                integracao = await _context.Integracoes.FindAsync(id);
            }

            if (integracao == null)
            {
                return NotFound(ApiResponse<IntegracaoDto>.Error("Integração não encontrada"));
            }

            return Ok(ApiResponse<IntegracaoDto>.Success(MapToDto(integracao, credentials)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar integração {Id}", id);
            return StatusCode(500, ApiResponse<IntegracaoDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Cria uma nova integração
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<IntegracaoDto>>> Create([FromBody] CreateIntegracaoRequest request)
    {
        try
        {
            _logger.LogInformation("[INTEGRACOES] Criando integração: {Nome}, {Canal}, {Provedor}",
                request.Nome, request.Canal, request.Provedor);
            
            // Log das credenciais recebidas (sem mostrar valores sensíveis)
            var credsLength = request.Credenciais?.Length ?? 0;
            _logger.LogInformation("[INTEGRACOES] Credenciais recebidas: {Length} caracteres, valor: {Credenciais}", 
                credsLength, request.Credenciais ?? "(null)");

            Integracao integracao;

            if (_settings.UseMocks)
            {
                _logger.LogInformation("[INTEGRACOES] Modo MOCK: Criando integração em memória");
                integracao = _mockStore.CreateIntegracao(new Integracao
                {
                    Nome = request.Nome,
                    Descricao = request.Descricao,
                    Tipo = string.IsNullOrEmpty(request.Tipo) ? request.Canal : request.Tipo,
                    Canal = request.Canal,
                    Provedor = request.Provedor,
                    UrlBase = request.UrlBase,
                    Credenciais = request.Credenciais ?? "{}",
                    Ativo = request.Ativo
                });
                _logger.LogInformation("[INTEGRACOES] Integração criada: {Id}, Credenciais salvas: {Creds}", 
                    integracao.Id, integracao.Credenciais);
            }
            else
            {
                integracao = new Integracao
                {
                    Nome = request.Nome,
                    Descricao = request.Descricao,
                    Tipo = string.IsNullOrEmpty(request.Tipo) ? request.Canal : request.Tipo,
                    Canal = request.Canal,
                    Provedor = request.Provedor,
                    UrlBase = request.UrlBase,
                    Credenciais = request.Credenciais ?? "{}",
                    Ativo = request.Ativo
                };

                _context.Integracoes.Add(integracao);
                await _context.SaveChangesAsync();
            }

            return StatusCode(201, ApiResponse<IntegracaoDto>.SuccessWithMessage(MapToDto(integracao, false), "Integração criada com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INTEGRACOES] Erro ao criar integração");
            return StatusCode(500, ApiResponse<IntegracaoDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Atualiza uma integração
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<IntegracaoDto>>> Update(string id, [FromBody] UpdateIntegracaoRequest request)
    {
        try
        {
            Integracao? integracao;

            if (_settings.UseMocks)
            {
                integracao = _mockStore.UpdateIntegracao(id, i =>
                {
                    if (!string.IsNullOrEmpty(request.Nome)) i.Nome = request.Nome;
                    if (request.Descricao != null) i.Descricao = request.Descricao;
                    if (!string.IsNullOrEmpty(request.Tipo)) i.Tipo = request.Tipo;
                    if (!string.IsNullOrEmpty(request.Canal)) i.Canal = request.Canal;
                    if (!string.IsNullOrEmpty(request.Provedor)) i.Provedor = request.Provedor;
                    if (request.UrlBase != null) i.UrlBase = request.UrlBase;
                    if (request.Credenciais != null) i.Credenciais = request.Credenciais;
                    if (request.Ativo.HasValue) i.Ativo = request.Ativo.Value;
                });
            }
            else
            {
                integracao = await _context.Integracoes.FindAsync(id);
                if (integracao == null)
                {
                    return NotFound(ApiResponse<IntegracaoDto>.Error("Integração não encontrada"));
                }

                if (!string.IsNullOrEmpty(request.Nome)) integracao.Nome = request.Nome;
                if (request.Descricao != null) integracao.Descricao = request.Descricao;
                if (!string.IsNullOrEmpty(request.Tipo)) integracao.Tipo = request.Tipo;
                if (!string.IsNullOrEmpty(request.Canal)) integracao.Canal = request.Canal;
                if (!string.IsNullOrEmpty(request.Provedor)) integracao.Provedor = request.Provedor;
                if (request.UrlBase != null) integracao.UrlBase = request.UrlBase;
                if (request.Credenciais != null) integracao.Credenciais = request.Credenciais;
                if (request.Ativo.HasValue) integracao.Ativo = request.Ativo.Value;

                await _context.SaveChangesAsync();
            }

            if (integracao == null)
            {
                return NotFound(ApiResponse<IntegracaoDto>.Error("Integração não encontrada"));
            }

            return Ok(ApiResponse<IntegracaoDto>.SuccessWithMessage(MapToDto(integracao, false), "Integração atualizada com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar integração {Id}", id);
            return StatusCode(500, ApiResponse<IntegracaoDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Remove uma integração
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        try
        {
            if (_settings.UseMocks)
            {
                var deleted = _mockStore.DeleteIntegracao(id);
                if (!deleted)
                {
                    return NotFound(ApiResponse<object>.Error("Integração não encontrada"));
                }
            }
            else
            {
                var integracao = await _context.Integracoes.FindAsync(id);
                if (integracao == null)
                {
                    return NotFound(ApiResponse<object>.Error("Integração não encontrada"));
                }

                _context.Integracoes.Remove(integracao);
                await _context.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.SuccessWithMessage(null, "Integração removida com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover integração {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Testa conexão com a integração
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<ApiResponse<TestIntegracaoResponse>>> Test(string id, [FromBody] TestIntegracaoRequest? request)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Integracao? integracao;

            if (_settings.UseMocks)
            {
                integracao = _mockStore.GetIntegracao(id);
            }
            else
            {
                integracao = await _context.Integracoes.FindAsync(id);
            }

            if (integracao == null)
            {
                return NotFound(ApiResponse<TestIntegracaoResponse>.Error("Integração não encontrada"));
            }

            _logger.LogInformation("[INTEGRACOES] Testando conexão da integração: {Nome} ({Id})", integracao.Nome, id);
            _logger.LogInformation("[INTEGRACOES] Credenciais da integração: {Credenciais}", integracao.Credenciais ?? "(null)");

            // Apenas testa integrações de email
            if (integracao.Canal != "email")
            {
                return Ok(ApiResponse<TestIntegracaoResponse>.SuccessWithMessage(new TestIntegracaoResponse
                {
                    IntegracaoId = id,
                    Status = "não testado",
                    Mock = _settings.UseMocks
                }, "Teste de conexão não implementado para este canal"));
            }

            // Testa conexão
            var connected = await _emailService.TestConnectionAsync(integracao.Credenciais);
            
            if (!connected)
            {
                stopwatch.Stop();
                return StatusCode(500, ApiResponse<TestIntegracaoResponse>.Error(
                    "Falha na conexão com o servidor de email", 
                    tempoProcessamento: stopwatch.ElapsedMilliseconds));
            }

            // Se forneceu email de teste, envia
            if (!string.IsNullOrEmpty(request?.EmailTeste))
            {
                _logger.LogInformation("[INTEGRACOES] Enviando email de teste para: {Email}", request.EmailTeste);
                var result = await _emailService.SendTestEmailAsync(integracao.Credenciais, request.EmailTeste);

                stopwatch.Stop();
                return Ok(ApiResponse<TestIntegracaoResponse>.SuccessWithMessage(
                    new TestIntegracaoResponse
                    {
                        IntegracaoId = id,
                        Status = result.Success ? "conectado" : "erro",
                        TempoResposta = stopwatch.ElapsedMilliseconds,
                        EmailTesteEnviado = result.Success,
                        EmailDestino = request.EmailTeste,
                        MessageId = result.MessageId,
                        Mock = false
                    },
                    result.Success 
                        ? $"Conexão testada e email de teste enviado para {request.EmailTeste}"
                        : result.Error ?? "Erro ao enviar email de teste",
                    tempoProcessamento: stopwatch.ElapsedMilliseconds));
            }

            stopwatch.Stop();
            return Ok(ApiResponse<TestIntegracaoResponse>.SuccessWithMessage(
                new TestIntegracaoResponse
                {
                    IntegracaoId = id,
                    Status = "conectado",
                    TempoResposta = stopwatch.ElapsedMilliseconds,
                    EmailTesteEnviado = false,
                    Mock = false
                },
                "Conexão testada com sucesso",
                tempoProcessamento: stopwatch.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[INTEGRACOES] Erro ao testar conexão");
            return StatusCode(500, ApiResponse<TestIntegracaoResponse>.Error(
                ex.Message, 
                tempoProcessamento: stopwatch.ElapsedMilliseconds));
        }
    }

    private static IntegracaoDto MapToDto(Integracao integracao, bool includeCredentials) => new()
    {
        Id = integracao.Id,
        Nome = integracao.Nome,
        Descricao = integracao.Descricao,
        Tipo = integracao.Tipo,
        Canal = integracao.Canal,
        Provedor = integracao.Provedor,
        UrlBase = integracao.UrlBase,
        Credenciais = includeCredentials ? integracao.Credenciais : null,
        Ativo = integracao.Ativo,
        CriadoEm = integracao.CriadoEm,
        AtualizadoEm = integracao.AtualizadoEm
    };
}

