using System.Diagnostics;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Usuario;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

/// <summary>Gestão de usuários do painel — lê e persiste em <c>tb_usuario</c> / <c>tb_perfil</c> (EF).</summary>
[ApiController]
[Authorize]
[Route("usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioAdminService _usuarios;

    public UsuariosController(IUsuarioAdminService usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>Lista utilizadores. Por omissão inclui ativos e inativos. Use <c>?apenasAtivos=true</c> para filtrar.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool apenasAtivos = false, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var list = await _usuarios.ListAsync(apenasAtivos, cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<UsuarioDto>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = list
        });
    }

    [HttpGet("perfis/list")]
    public async Task<IActionResult> Perfis(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var list = await _usuarios.ListPerfisAsync(cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<PerfilDto>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = list
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest? body, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();
        if (body is null || string.IsNullOrWhiteSpace(body.Senha))
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "Dados inválidos.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }

        try
        {
            var u = await _usuarios.CreateAsync(body, cancellationToken);
            return StatusCode(201, new ApiResponse<UsuarioDto>
            {
                Sucesso = true,
                Mensagem = "Usuário criado.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds,
                Resultado = u
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUsuarioRequest? body, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();
        if (body is null)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "Body inválido.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }

        try
        {
            var u = await _usuarios.UpdateAsync(id, body, cancellationToken);
            if (u is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Sucesso = false,
                    Mensagem = "Usuário não encontrado.",
                    RequisicaoId = rid,
                    TempoProcessamento = sw.ElapsedMilliseconds
                });
            }

            return Ok(new ApiResponse<UsuarioDto>
            {
                Sucesso = true,
                Mensagem = "Usuário atualizado.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds,
                Resultado = u
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = ex.Message,
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();
        if (!await _usuarios.DeactivateAsync(id, cancellationToken))
        {
            return NotFound(new ApiResponse<object>
            {
                Sucesso = false,
                Mensagem = "Usuário não encontrado.",
                RequisicaoId = rid,
                TempoProcessamento = sw.ElapsedMilliseconds
            });
        }

        return Ok(new ApiResponse<object?>
        {
            Sucesso = true,
            Mensagem = "Usuário desativado.",
            RequisicaoId = rid,
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = null
        });
    }
}
