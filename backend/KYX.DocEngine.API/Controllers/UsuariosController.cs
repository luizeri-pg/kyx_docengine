using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KYX.NotifyHUB.API.Configuration;
using KYX.NotifyHUB.API.Data;
using KYX.NotifyHUB.API.Models.DTOs;
using KYX.NotifyHUB.API.Models.DTOs.Usuario;
using KYX.NotifyHUB.API.Models.Entities;
using KYX.NotifyHUB.API.Stores;
using Microsoft.Extensions.Options;

namespace KYX.NotifyHUB.API.Controllers;

[ApiController]
[Route("usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly MockStore _mockStore;
    private readonly AppSettings _settings;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(
        AppDbContext context,
        MockStore mockStore,
        IOptions<AppSettings> settings,
        ILogger<UsuariosController> logger)
    {
        _context = context;
        _mockStore = mockStore;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os usuários
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UsuarioDto>>>> GetAll()
    {
        try
        {
            List<UsuarioDto> resultado;

            if (_settings.UseMocks)
            {
                var usuarios = _mockStore.ListUsuarios();
                resultado = usuarios.Select(MapToDto).ToList();
            }
            else
            {
                var usuarios = await _context.Usuarios
                    .Include(u => u.Perfil)
                    .OrderByDescending(u => u.CriadoEm)
                    .ToListAsync();

                resultado = usuarios.Select(MapToDto).ToList();
            }

            return Ok(ApiResponse<List<UsuarioDto>>.Success(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar usuários");
            return StatusCode(500, ApiResponse<List<UsuarioDto>>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Busca um usuário por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> GetById(string id)
    {
        try
        {
            Usuario? usuario;

            if (_settings.UseMocks)
            {
                usuario = _mockStore.GetUsuario(id);
            }
            else
            {
                usuario = await _context.Usuarios
                    .Include(u => u.Perfil)
                    .FirstOrDefaultAsync(u => u.Id == id);
            }

            if (usuario == null)
            {
                return NotFound(ApiResponse<UsuarioDto>.Error("Usuário não encontrado"));
            }

            return Ok(ApiResponse<UsuarioDto>.Success(MapToDto(usuario)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuário {Id}", id);
            return StatusCode(500, ApiResponse<UsuarioDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Cria um novo usuário
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> Create([FromBody] CreateUsuarioRequest request)
    {
        try
        {
            var senhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha);

            Usuario usuario;

            if (_settings.UseMocks)
            {
                usuario = _mockStore.CreateUsuario(new Usuario
                {
                    Nome = request.Nome,
                    Email = request.Email,
                    Senha = senhaHash,
                    PerfilId = request.PerfilId,
                    Ativo = request.Ativo
                });
            }
            else
            {
                usuario = new Usuario
                {
                    Nome = request.Nome,
                    Email = request.Email,
                    Senha = senhaHash,
                    PerfilId = request.PerfilId,
                    Ativo = request.Ativo
                };

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                // Recarrega com perfil
                usuario = await _context.Usuarios
                    .Include(u => u.Perfil)
                    .FirstAsync(u => u.Id == usuario.Id);
            }

            return StatusCode(201, ApiResponse<UsuarioDto>.SuccessWithMessage(MapToDto(usuario), "Usuário criado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
            return StatusCode(500, ApiResponse<UsuarioDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Atualiza um usuário
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<UsuarioDto>>> Update(string id, [FromBody] UpdateUsuarioRequest request)
    {
        try
        {
            Usuario? usuario;

            if (_settings.UseMocks)
            {
                usuario = _mockStore.UpdateUsuario(id, u =>
                {
                    if (!string.IsNullOrEmpty(request.Nome)) u.Nome = request.Nome;
                    if (!string.IsNullOrEmpty(request.Email)) u.Email = request.Email;
                    if (!string.IsNullOrEmpty(request.Senha)) u.Senha = BCrypt.Net.BCrypt.HashPassword(request.Senha);
                    if (!string.IsNullOrEmpty(request.PerfilId)) u.PerfilId = request.PerfilId;
                    if (request.Ativo.HasValue) u.Ativo = request.Ativo.Value;
                });
            }
            else
            {
                usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<UsuarioDto>.Error("Usuário não encontrado"));
                }

                if (!string.IsNullOrEmpty(request.Nome)) usuario.Nome = request.Nome;
                if (!string.IsNullOrEmpty(request.Email)) usuario.Email = request.Email;
                if (!string.IsNullOrEmpty(request.Senha)) usuario.Senha = BCrypt.Net.BCrypt.HashPassword(request.Senha);
                if (!string.IsNullOrEmpty(request.PerfilId)) usuario.PerfilId = request.PerfilId;
                if (request.Ativo.HasValue) usuario.Ativo = request.Ativo.Value;

                await _context.SaveChangesAsync();

                // Recarrega com perfil
                await _context.Entry(usuario).Reference(u => u.Perfil).LoadAsync();
            }

            if (usuario == null)
            {
                return NotFound(ApiResponse<UsuarioDto>.Error("Usuário não encontrado"));
            }

            return Ok(ApiResponse<UsuarioDto>.SuccessWithMessage(MapToDto(usuario), "Usuário atualizado com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar usuário {Id}", id);
            return StatusCode(500, ApiResponse<UsuarioDto>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Remove um usuário
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        try
        {
            if (_settings.UseMocks)
            {
                var deleted = _mockStore.DeleteUsuario(id);
                if (!deleted)
                {
                    return NotFound(ApiResponse<object>.Error("Usuário não encontrado"));
                }
            }
            else
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    return NotFound(ApiResponse<object>.Error("Usuário não encontrado"));
                }

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.SuccessWithMessage(null, "Usuário removido com sucesso"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover usuário {Id}", id);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>
    /// Lista todos os perfis
    /// </summary>
    [HttpGet("perfis/list")]
    public async Task<ActionResult<ApiResponse<List<PerfilDto>>>> GetPerfis()
    {
        try
        {
            List<PerfilDto> resultado;

            if (_settings.UseMocks)
            {
                var perfis = _mockStore.ListPerfis();
                resultado = perfis.Select(p => new PerfilDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Descricao = p.Descricao,
                    CriadoEm = p.CriadoEm,
                    AtualizadoEm = p.AtualizadoEm
                }).ToList();
            }
            else
            {
                var perfis = await _context.Perfis
                    .OrderBy(p => p.Nome)
                    .ToListAsync();

                resultado = perfis.Select(p => new PerfilDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Descricao = p.Descricao,
                    CriadoEm = p.CriadoEm,
                    AtualizadoEm = p.AtualizadoEm
                }).ToList();
            }

            return Ok(ApiResponse<List<PerfilDto>>.Success(resultado));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar perfis");
            return StatusCode(500, ApiResponse<List<PerfilDto>>.Error(ex.Message));
        }
    }

    private static UsuarioDto MapToDto(Usuario usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Nome,
        Email = usuario.Email,
        PerfilId = usuario.PerfilId,
        Ativo = usuario.Ativo,
        CriadoEm = usuario.CriadoEm,
        AtualizadoEm = usuario.AtualizadoEm,
        Perfil = usuario.Perfil == null ? null : new PerfilDto
        {
            Id = usuario.Perfil.Id,
            Nome = usuario.Perfil.Nome,
            Descricao = usuario.Perfil.Descricao,
            CriadoEm = usuario.Perfil.CriadoEm,
            AtualizadoEm = usuario.Perfil.AtualizadoEm
        }
    };
}

