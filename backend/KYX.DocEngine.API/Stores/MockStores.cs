using System.Collections.Concurrent;
using KYX.NotifyHUB.API.Models.Entities;

namespace KYX.NotifyHUB.API.Stores;

/// <summary>
/// Store em memória para modo mock (desenvolvimento/testes)
/// </summary>
public class MockStore
{
    // Usuários
    private readonly ConcurrentDictionary<string, Usuario> _usuarios = new();
    
    // Perfis
    private readonly ConcurrentDictionary<string, Perfil> _perfis = new();
    
    // Roles
    private readonly ConcurrentDictionary<string, Role> _roles = new();
    
    // Integrações
    private readonly ConcurrentDictionary<string, Integracao> _integracoes = new();
    
    // Logs
    private readonly ConcurrentDictionary<string, LogRequisicao> _logRequisicoes = new();
    private readonly ConcurrentDictionary<string, LogIntegracao> _logIntegracoes = new();

    public MockStore()
    {
        SeedData();
    }

    private void SeedData()
    {
        // Criar perfis
        var perfilAdmin = new Perfil
        {
            Id = "perfil-admin",
            Nome = "Administrador",
            Descricao = "Acesso total ao sistema"
        };
        var perfilUser = new Perfil
        {
            Id = "perfil-user",
            Nome = "Usuário",
            Descricao = "Acesso básico"
        };
        _perfis[perfilAdmin.Id] = perfilAdmin;
        _perfis[perfilUser.Id] = perfilUser;

        // Criar roles
        var roleAdmin = new Role
        {
            Id = "role-admin",
            Nome = "admin",
            Descricao = "Administrador"
        };
        var roleUser = new Role
        {
            Id = "role-user",
            Nome = "user",
            Descricao = "Usuário comum"
        };
        _roles[roleAdmin.Id] = roleAdmin;
        _roles[roleUser.Id] = roleUser;

        // Adicionar roles aos perfis
        perfilAdmin.PerfilRoles.Add(new PerfilRole { PerfilId = perfilAdmin.Id, RoleId = roleAdmin.Id, Role = roleAdmin });
        perfilUser.PerfilRoles.Add(new PerfilRole { PerfilId = perfilUser.Id, RoleId = roleUser.Id, Role = roleUser });

        // Criar usuário admin (senha: admin123)
        var admin = new Usuario
        {
            Id = "user-admin",
            Nome = "admin",
            Email = "admin@notifyhub.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("admin123"),
            PerfilId = perfilAdmin.Id,
            Perfil = perfilAdmin,
            Ativo = true
        };
        _usuarios[admin.Id] = admin;

        // Criar integrações de exemplo
        var integracaoEmail = new Integracao
        {
            Id = "int-email-smtp",
            Nome = "SMTP Principal",
            Tipo = "email",
            Canal = "email",
            Provedor = "smtp",
            Credenciais = "{}",
            Ativo = true
        };
        _integracoes[integracaoEmail.Id] = integracaoEmail;
    }

    // === Usuários ===
    public List<Usuario> ListUsuarios() => _usuarios.Values.ToList();
    public Usuario? GetUsuario(string id) => _usuarios.GetValueOrDefault(id);
    public Usuario? GetUsuarioByEmail(string email) => _usuarios.Values.FirstOrDefault(u => u.Email == email);
    public Usuario? GetUsuarioByUsername(string username) => _usuarios.Values.FirstOrDefault(u => u.Nome == username || u.Email == username);
    
    public Usuario CreateUsuario(Usuario usuario)
    {
        usuario.Id = $"user-{DateTime.UtcNow.Ticks}";
        usuario.CriadoEm = DateTime.UtcNow;
        usuario.AtualizadoEm = DateTime.UtcNow;
        usuario.Perfil = GetPerfil(usuario.PerfilId);
        _usuarios[usuario.Id] = usuario;
        return usuario;
    }
    
    public Usuario? UpdateUsuario(string id, Action<Usuario> update)
    {
        if (!_usuarios.TryGetValue(id, out var usuario)) return null;
        update(usuario);
        usuario.AtualizadoEm = DateTime.UtcNow;
        return usuario;
    }
    
    public bool DeleteUsuario(string id) => _usuarios.TryRemove(id, out _);

    // === Perfis ===
    public List<Perfil> ListPerfis() => _perfis.Values.ToList();
    public Perfil? GetPerfil(string id) => _perfis.GetValueOrDefault(id);

    // === Integrações ===
    public List<Integracao> ListIntegracoes(string? canal = null, bool? ativo = null)
    {
        var query = _integracoes.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(canal))
            query = query.Where(i => i.Canal == canal);
        if (ativo.HasValue)
            query = query.Where(i => i.Ativo == ativo.Value);
        return query.OrderByDescending(i => i.CriadoEm).ToList();
    }
    
    public Integracao? GetIntegracao(string id) => _integracoes.GetValueOrDefault(id);
    
    public Integracao CreateIntegracao(Integracao integracao)
    {
        integracao.Id = $"int-{DateTime.UtcNow.Ticks}";
        integracao.CriadoEm = DateTime.UtcNow;
        integracao.AtualizadoEm = DateTime.UtcNow;
        _integracoes[integracao.Id] = integracao;
        return integracao;
    }
    
    public Integracao? UpdateIntegracao(string id, Action<Integracao> update)
    {
        if (!_integracoes.TryGetValue(id, out var integracao)) return null;
        update(integracao);
        integracao.AtualizadoEm = DateTime.UtcNow;
        return integracao;
    }
    
    public bool DeleteIntegracao(string id) => _integracoes.TryRemove(id, out _);

    // === Logs ===
    public List<LogRequisicao> ListLogRequisicoes(string? canal = null, string? centroCusto = null, int limit = 100, int offset = 0)
    {
        var query = _logRequisicoes.Values.AsEnumerable();
        if (!string.IsNullOrEmpty(canal))
            query = query.Where(l => l.Canal == canal);
        if (!string.IsNullOrEmpty(centroCusto))
            query = query.Where(l => l.CentroCusto == centroCusto);
        return query.OrderByDescending(l => l.CriadoEm).Skip(offset).Take(limit).ToList();
    }
    
    public LogRequisicao? GetLogRequisicao(string requisicaoId) => 
        _logRequisicoes.Values.FirstOrDefault(l => l.RequisicaoId == requisicaoId);
    
    public LogRequisicao CreateLogRequisicao(LogRequisicao log)
    {
        log.Id = $"log-{DateTime.UtcNow.Ticks}";
        log.CriadoEm = DateTime.UtcNow;
        _logRequisicoes[log.Id] = log;
        return log;
    }
    
    public LogRequisicao? UpdateLogRequisicao(string requisicaoId, Action<LogRequisicao> update)
    {
        var log = _logRequisicoes.Values.FirstOrDefault(l => l.RequisicaoId == requisicaoId);
        if (log == null) return null;
        update(log);
        return log;
    }

    public List<LogIntegracao> GetLogIntegracoesByRequisicao(string requisicaoId) =>
        _logIntegracoes.Values.Where(l => l.RequisicaoId == requisicaoId).ToList();
    
    public LogIntegracao CreateLogIntegracao(LogIntegracao log)
    {
        log.Id = $"logint-{DateTime.UtcNow.Ticks}";
        log.CriadoEm = DateTime.UtcNow;
        _logIntegracoes[log.Id] = log;
        return log;
    }

    public (int total, int sucesso, int erros, Dictionary<string, int> porCanal) GetStats()
    {
        var logs = _logRequisicoes.Values.ToList();
        var total = logs.Count;
        var sucesso = logs.Count(l => l.StatusHttp == 200);
        var erros = logs.Count(l => !string.IsNullOrEmpty(l.Erro) || (l.StatusHttp.HasValue && l.StatusHttp >= 400));
        var porCanal = logs.GroupBy(l => l.Canal).ToDictionary(g => g.Key, g => g.Count());
        return (total, sucesso, erros, porCanal);
    }
}

