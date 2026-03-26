using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using KYX.DocEngine.API.Models.DTOs.Usuario;

namespace KYX.DocEngine.API.Services;

/// <summary>
/// Armazenamento em memória para gestão de usuários no painel DocEngine (sem tabela dedicada no banco atual).
/// </summary>
public sealed class InMemoryUsuarioStore
{
    private readonly ConcurrentDictionary<string, UsuarioInterno> _usuarios = new();
    private readonly ConcurrentDictionary<string, PerfilDto> _perfis = new();

    public InMemoryUsuarioStore()
    {
        var agora = DateTime.UtcNow;
        var pAdmin = new PerfilDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Nome = "ADMIN",
            Descricao = "Administrador DocEngine",
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        var pIntegracao = new PerfilDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Nome = "INTEGRAÇÃO",
            Descricao = "Acesso API / integrações",
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        _perfis[pAdmin.Id] = pAdmin;
        _perfis[pIntegracao.Id] = pIntegracao;

        void AddUser(string nome, string email, string perfilId, bool ativo)
        {
            var id = Guid.NewGuid().ToString("N");
            _usuarios[id] = new UsuarioInterno
            {
                Id = id,
                Nome = nome,
                Email = email,
                SenhaHash = HashPassword("docengine123"),
                PerfilId = perfilId,
                Ativo = ativo,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
        }

        AddUser("Usuário Demo", "demo@kyx.local", pIntegracao.Id, true);
        AddUser("Admin Painel", "admin.painel@kyx.local", pAdmin.Id, true);
    }

    public IReadOnlyList<PerfilDto> ListPerfis() => _perfis.Values.OrderBy(p => p.Nome).ToList();

    public IReadOnlyList<UsuarioDto> ListUsuarios() =>
        _usuarios.Values.OrderByDescending(u => u.CriadoEm).Select(MapToDto).ToList();

    public UsuarioDto? GetById(string id) =>
        _usuarios.TryGetValue(id, out var u) ? MapToDto(u) : null;

    public UsuarioDto Create(CreateUsuarioRequest req)
    {
        if (!_perfis.ContainsKey(req.PerfilId))
            throw new InvalidOperationException("Perfil inválido.");

        if (_usuarios.Values.Any(x => x.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Já existe usuário com este email.");

        var id = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var inner = new UsuarioInterno
        {
            Id = id,
            Nome = req.Nome.Trim(),
            Email = req.Email.Trim(),
            SenhaHash = HashPassword(req.Senha),
            PerfilId = req.PerfilId,
            Ativo = req.Ativo,
            CriadoEm = now,
            AtualizadoEm = now
        };
        _usuarios[id] = inner;
        return MapToDto(inner);
    }

    public UsuarioDto? Update(string id, UpdateUsuarioRequest req)
    {
        if (!_usuarios.TryGetValue(id, out var u))
            return null;

        if (!string.IsNullOrWhiteSpace(req.Nome)) u.Nome = req.Nome.Trim();
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            if (_usuarios.Values.Any(x => x.Id != id && x.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Já existe usuário com este email.");
            u.Email = req.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(req.PerfilId))
        {
            if (!_perfis.ContainsKey(req.PerfilId))
                throw new InvalidOperationException("Perfil inválido.");
            u.PerfilId = req.PerfilId;
        }

        if (req.Ativo.HasValue) u.Ativo = req.Ativo.Value;
        if (!string.IsNullOrWhiteSpace(req.Senha)) u.SenhaHash = HashPassword(req.Senha);
        u.AtualizadoEm = DateTime.UtcNow;
        return MapToDto(u);
    }

    public bool Delete(string id) => _usuarios.TryRemove(id, out _);

    private UsuarioDto MapToDto(UsuarioInterno u)
    {
        _perfis.TryGetValue(u.PerfilId, out var perfil);
        return new UsuarioDto
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            PerfilId = u.PerfilId,
            Ativo = u.Ativo,
            CriadoEm = u.CriadoEm,
            AtualizadoEm = u.AtualizadoEm,
            Perfil = perfil
        };
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + ":kyx-docengine-users"));
        return Convert.ToHexString(bytes);
    }

    private sealed class UsuarioInterno
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Email { get; set; } = "";
        public string SenhaHash { get; set; } = "";
        public string PerfilId { get; set; } = "";
        public bool Ativo { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime AtualizadoEm { get; set; }
    }
}
