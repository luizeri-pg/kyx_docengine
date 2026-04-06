using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.Entities;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace KYX.DocEngine.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly bool _persistTbLog;
    private static int _loggedMissingTable;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _persistTbLog = configuration.GetValue("Logging:PersistTbLogRequisicao", true);
    }

    public async Task InvokeAsync(HttpContext context, DocEngineDbContext db)
    {
        var stopwatch = Stopwatch.StartNew();

        // GET/HEAD sem corpo: não ler stream (evita falhas com proxy / stream não seekable → 500).
        var requestBody = "";
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Request.EnableBuffering();
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            if (context.Request.Body.CanSeek)
                context.Request.Body.Position = 0;
        }

        // GET/HEAD: não substituir Response.Body — evita 500 opacos com proxy/Kestrel e não precisamos do corpo para auditoria.
        var captureResponse = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        string responseBody;
        if (captureResponse)
        {
            var originalBody = context.Response.Body;
            await using var responseBuffer = new MemoryStream();
            context.Response.Body = responseBuffer;
            responseBody = "";
            try
            {
                await _next(context);
            }
            finally
            {
                responseBuffer.Position = 0;
                responseBody = await new StreamReader(responseBuffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true).ReadToEndAsync();
                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalBody);
                context.Response.Body = originalBody;
            }
        }
        else
        {
            responseBody = "";
            await _next(context);
        }

        stopwatch.Stop();
        string? requisicaoId = null;
        string? centroCusto = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                var json = JsonDocument.Parse(requestBody);
                if (json.RootElement.TryGetProperty("requisicaoId", out var rid))
                {
                    requisicaoId = rid.GetString();
                }

                if (json.RootElement.TryGetProperty("config", out var config)
                    && config.TryGetProperty("centroCusto", out var cc))
                {
                    centroCusto = cc.GetString();
                }
            }
        }
        catch
        {
            // Não interrompe o request por erro de parse de log.
        }

        var ridFinal = requisicaoId ?? Guid.NewGuid().ToString();
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var ms = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

        if (!_persistTbLog)
        {
            return;
        }

        try
        {
            var log = new LogRequisicao
            {
                Id = Guid.NewGuid().ToString(),
                RequisicaoId = ridFinal,
                UsuarioId = userId,
                Canal = "docengine",
                CentroCusto = centroCusto,
                RequestPayload = AuditPayloadHelper.ToRequestPayloadJson(path, method, requestBody),
                ResponsePayload = AuditPayloadHelper.ToResponsePayloadJson(responseBody),
                StatusHttp = context.Response.StatusCode,
                TempoRespostaMs = ms,
                Erro = context.Response.StatusCode >= 400 ? $"HTTP {context.Response.StatusCode}" : null,
                CriadoEm = DateTime.UtcNow
            };

            db.LogRequisicoes.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            if (IsRelationMissing(ex))
            {
                if (Interlocked.Exchange(ref _loggedMissingTable, 1) == 0)
                {
                    _logger.LogWarning(
                        ex,
                        "Tabela tb_log_requisicao ausente ou sem permissão. Aplique migrações ou ajuste o PostgreSQL. " +
                        "Próximas falhas de persistência serão omitidas deste log.");
                }
                else
                {
                    _logger.LogDebug(ex, "tb_log_requisicao indisponível para {Path}", context.Request.Path);
                }
            }
            else
            {
                _logger.LogWarning(ex, "Falha ao persistir tb_log_requisicao para {Path}", context.Request.Path);
            }
        }
    }

    private static bool IsRelationMissing(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState == "42P01")
                return true;
        }

        return false;
    }
}
