using System.Globalization;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.DTOs.Usuario;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Services;

public interface IUsuarioAdminService
{
    /// <param name="apenasAtivos">Se <c>true</c>, só utilizadores com <c>Ativo == true</c>. Se <c>false</c> (omissão), devolve <b>todos</b> os registos de <c>tb_usuario</c>.</param>
    Task<IReadOnlyList<UsuarioDto>> ListAsync(bool apenasAtivos = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PerfilDto>> ListPerfisAsync(CancellationToken cancellationToken = default);
    Task<UsuarioDto> CreateAsync(CreateUsuarioRequest req, CancellationToken cancellationToken = default);
    Task<UsuarioDto?> UpdateAsync(string id, UpdateUsuarioRequest req, CancellationToken cancellationToken = default);
    /// <summary>Desativa o utilizador (<c>Ativo = false</c>), não apaga linha na BD.</summary>
    Task<bool> DeactivateAsync(string id, CancellationToken cancellationToken = default);
}

public sealed class UsuarioAdminService : IUsuarioAdminService
{
    private readonly DocEngineDbContext _db;
    private readonly SchemaTableOptions _schema;
    private readonly ILogger<UsuarioAdminService> _logger;

    public UsuarioAdminService(
        DocEngineDbContext db,
        IOptions<SchemaTableOptions> schemaOptions,
        ILogger<UsuarioAdminService> logger)
    {
        _db = db;
        _schema = schemaOptions.Value;
        _logger = logger;
    }

    private bool MapsPerfil => !string.IsNullOrWhiteSpace(_schema.Usuario.PerfilId);
    private bool MapsLogin => !string.IsNullOrWhiteSpace(_schema.Usuario.Login);

    /// <summary>Quando <c>CriadoEm</c> e <c>AtualizadoEm</c> mapeiam a mesma coluna, o EF ignora <c>AtualizadoEm</c> — não usar em LINQ para SQL.</summary>
    private bool UsuarioEmColunaUnicaDeData =>
        string.Equals(_schema.Usuario.CriadoEm, _schema.Usuario.AtualizadoEm, StringComparison.OrdinalIgnoreCase);

    private bool PerfilEmColunaUnicaDeData =>
        string.Equals(_schema.Perfil.CriadoEm, _schema.Perfil.AtualizadoEm, StringComparison.OrdinalIgnoreCase);

    private bool MapsPerfilDescricaoColuna =>
        !string.IsNullOrWhiteSpace(_schema.Perfil.Descricao);

    public async Task<IReadOnlyList<UsuarioDto>> ListAsync(bool apenasAtivos = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Usuario> q = _db.Usuarios.AsNoTracking();
        if (apenasAtivos)
            q = q.Where(u => u.Ativo);
        if (MapsPerfil)
            q = q.Include(u => u.Perfil);

        q = UsuarioEmColunaUnicaDeData
            ? q.OrderByDescending(u => u.CriadoEm)
            : q.OrderByDescending(u => u.AtualizadoEm).ThenByDescending(u => u.CriadoEm);

        try
        {
            var list = await q.ToListAsync(cancellationToken);
            return list.Select(MapToDto).ToList();
        }
        catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
        {
            _logger.LogWarning(ex, "Tabela «tb_usuario» ausente ou inacessível; devolvendo lista vazia.");
            return Array.Empty<UsuarioDto>();
        }
    }

    public async Task<IReadOnlyList<PerfilDto>> ListPerfisAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Materializa em memória: evita falhas de tradução com Descricao ignorada no EF e colunas NULL legadas.
            var rows = await _db.Perfis.AsNoTracking()
                .OrderBy(p => p.Nome ?? "")
                .ToListAsync(cancellationToken);
            return rows.Select(MapPerfilRowToDto).ToList();
        }
        catch (Exception ex) when (PostgresErrors.IsUndefinedTable(ex))
        {
            _logger.LogWarning(ex, "Tabela «tb_perfil» ausente ou inacessível; devolvendo lista vazia.");
            return Array.Empty<PerfilDto>();
        }
    }

    private PerfilDto MapPerfilRowToDto(Perfil p)
    {
        var criado = p.CriadoEm ?? default;
        var atual = PerfilEmColunaUnicaDeData ? criado : (p.AtualizadoEm ?? criado);
        return new PerfilDto
        {
            Id = p.Id,
            Nome = p.Nome ?? string.Empty,
            Descricao = MapsPerfilDescricaoColuna ? p.Descricao : null,
            CriadoEm = criado,
            AtualizadoEm = atual,
        };
    }

    public async Task<UsuarioDto> CreateAsync(CreateUsuarioRequest req, CancellationToken cancellationToken = default)
    {
        var email = req.Email.Trim();
        if (await EmailEmUsoPorOutroAsync(null, email, cancellationToken))
            throw new InvalidOperationException("Já existe usuário com este e-mail.");

        if (MapsPerfil && string.IsNullOrWhiteSpace(req.PerfilId))
            throw new InvalidOperationException("Perfil é obrigatório.");

        if (MapsPerfil)
        {
            var perfilOk = await _db.Perfis.AnyAsync(p => p.Id == req.PerfilId, cancellationToken);
            if (!perfilOk)
                throw new InvalidOperationException("Perfil inválido.");
        }

        var id = await GerarNovoIdAsync(cancellationToken);
        var agora = DateTime.UtcNow;
        var senhaHash = BCrypt.Net.BCrypt.HashPassword(req.Senha);

        var entity = new Usuario
        {
            Id = id,
            Nome = req.Nome.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            Senha = senhaHash,
            PerfilId = MapsPerfil ? req.PerfilId : string.Empty,
            Ativo = req.Ativo,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        if (MapsLogin)
            entity.Login = string.IsNullOrWhiteSpace(email) ? req.Nome.Trim() : email;

        _db.Usuarios.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToDto(await CarregarComPerfilAsync(entity.Id, cancellationToken) ?? entity);
    }

    public async Task<UsuarioDto?> UpdateAsync(string id, UpdateUsuarioRequest req, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (entity is null)
            return null;

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var email = req.Email.Trim();
            if (await EmailEmUsoPorOutroAsync(id, email, cancellationToken))
                throw new InvalidOperationException("Já existe usuário com este e-mail.");
            entity.Email = string.IsNullOrWhiteSpace(email) ? null : email;
            if (MapsLogin)
                entity.Login = entity.Email ?? entity.Login;
        }

        if (!string.IsNullOrWhiteSpace(req.Nome))
            entity.Nome = req.Nome.Trim();

        if (MapsPerfil && !string.IsNullOrWhiteSpace(req.PerfilId))
        {
            var perfilOk = await _db.Perfis.AnyAsync(p => p.Id == req.PerfilId, cancellationToken);
            if (!perfilOk)
                throw new InvalidOperationException("Perfil inválido.");
            entity.PerfilId = req.PerfilId;
        }

        if (req.Ativo.HasValue)
            entity.Ativo = req.Ativo.Value;

        if (!string.IsNullOrWhiteSpace(req.Senha))
            entity.Senha = BCrypt.Net.BCrypt.HashPassword(req.Senha);

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(await CarregarComPerfilAsync(id, cancellationToken) ?? entity);
    }

    public async Task<bool> DeactivateAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (entity is null)
            return false;
        entity.Ativo = false;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> GerarNovoIdAsync(CancellationToken cancellationToken)
    {
        if (!_schema.Usuario.IdIntegerType)
            return Guid.NewGuid().ToString("N");

        if (!await _db.Usuarios.AnyAsync(cancellationToken))
            return "1";

        var ids = await _db.Usuarios.Select(u => u.Id).ToListAsync(cancellationToken);
        var max = ids.Max(x => int.Parse(x, CultureInfo.InvariantCulture));
        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<bool> EmailEmUsoPorOutroAsync(string? excetoId, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var e = email.Trim();
        return await _db.Usuarios.AnyAsync(
            u =>
                (excetoId == null || u.Id != excetoId) &&
                u.Email != null &&
                u.Email.ToLower() == e.ToLower(),
            cancellationToken);
    }

    private async Task<Usuario?> CarregarComPerfilAsync(string id, CancellationToken cancellationToken)
    {
        IQueryable<Usuario> q = _db.Usuarios.AsNoTracking();
        if (MapsPerfil)
            q = q.Include(u => u.Perfil);
        return await q.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    private UsuarioDto MapToDto(Usuario u)
    {
        var atualUsuario = UsuarioEmColunaUnicaDeData ? u.CriadoEm : u.AtualizadoEm;
        return new UsuarioDto
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email ?? string.Empty,
            PerfilId = MapsPerfil ? u.PerfilId : string.Empty,
            Ativo = u.Ativo,
            CriadoEm = u.CriadoEm,
            AtualizadoEm = atualUsuario,
            Perfil = u.Perfil is null ? null : MapPerfilRowToDto(u.Perfil),
        };
    }
}
