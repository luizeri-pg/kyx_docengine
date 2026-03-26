using System.Diagnostics;
using KYX.DocEngine.API.Models.DTOs;
using KYX.DocEngine.API.Models.DTOs.Usuario;
using KYX.DocEngine.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KYX.DocEngine.API.Controllers;

/// <summary>Gestão de usuários do painel (armazenamento em memória no DocEngine).</summary>
[ApiController]
[Authorize]
[Route("usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly InMemoryUsuarioStore _store;

    public UsuariosController(InMemoryUsuarioStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult List()
    {
        var sw = Stopwatch.StartNew();
        var list = _store.ListUsuarios();
        return Ok(new ApiResponse<IReadOnlyList<UsuarioDto>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = list
        });
    }

    [HttpGet("perfis/list")]
    public IActionResult Perfis()
    {
        var sw = Stopwatch.StartNew();
        var list = _store.ListPerfis();
        return Ok(new ApiResponse<IReadOnlyList<PerfilDto>>
        {
            Sucesso = true,
            RequisicaoId = Guid.NewGuid().ToString(),
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = list
        });
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateUsuarioRequest? body)
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
            var u = _store.Create(body);
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
    public IActionResult Update(string id, [FromBody] UpdateUsuarioRequest? body)
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
            var u = _store.Update(id, body);
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
    public IActionResult Delete(string id)
    {
        var sw = Stopwatch.StartNew();
        var rid = Guid.NewGuid().ToString();
        if (!_store.Delete(id))
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
            Mensagem = "Usuário removido.",
            RequisicaoId = rid,
            TempoProcessamento = sw.ElapsedMilliseconds,
            Resultado = null
        });
    }
}
