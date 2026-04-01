using System.Text;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.Redis.StackExchange;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Data;
using KYX.DocEngine.API.Filters;
using KYX.DocEngine.API.Middleware;
using KYX.DocEngine.API.Services;
using KYX.DocEngine.API.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
// Sobrescreve secrets/local (não versionar: appsettings.Local.json está no .gitignore)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<SchemaTableOptions>(builder.Configuration.GetSection("Schema"));

builder.Services.AddDbContext<DocEngineDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("KYX.DocEngine.API"))
        .UseSnakeCaseNamingConvention()
        // O snapshot de migrações não espelha o mapeamento dinâmico em Schema:* (appsettings); evita falhar Migrate() em dev.
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Hangfire: Redis (produção) ou Memory (dev sem Redis local). Ver appsettings.Development.json
var hangfireStorage = builder.Configuration["Hangfire:Storage"] ?? "Redis";
builder.Services.AddHangfire(config =>
{
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings();

    if (string.Equals(hangfireStorage, "Memory", StringComparison.OrdinalIgnoreCase))
    {
        config.UseMemoryStorage();
    }
    else
    {
        var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        if (!redisConnection.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
        {
            redisConnection = redisConnection.TrimEnd(';').Trim() + ",abortConnect=false";
        }

        config.UseRedisStorage(redisConnection, new RedisStorageOptions
        {
            Prefix = "docengine:",
            InvisibilityTimeout = TimeSpan.FromMinutes(10)
        });
    }
});
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.Queues = new[] { "documents", "default" };
});

builder.Services.AddScoped<IUsuarioAdminService, UsuarioAdminService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentJobService, DocumentJobService>();
builder.Services.AddScoped<IHtmlPdfRenderer, HtmlPdfRenderer>();
builder.Services.AddScoped<IAcroFormPdfRenderer, AcroFormPdfRenderer>();
builder.Services.AddScoped<IPdfEngineService, PdfEngineService>();
builder.Services.AddScoped<IDocumentWorker, DocumentWorker>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:5174" };
// Seção vazia no JSON vira [] (não null) — sem isso o CORS não libera nenhuma origem.
if (corsOrigins.Length == 0)
{
    corsOrigins = new[] { "http://localhost:5173", "http://localhost:5174" };
}

// Mesmas origens com 127.0.0.1 (alguns browsers / Vite usam isso no Origin)
var corsOriginsExpanded = corsOrigins
    .SelectMany(o =>
    {
        try
        {
            var u = new Uri(o);
            if (u.Host == "localhost")
            {
                var alt = new UriBuilder(u) { Host = "127.0.0.1" }.Uri.ToString().TrimEnd('/');
                return new[] { o.TrimEnd('/'), alt };
            }
        }
        catch
        {
            /* ignore */
        }

        return new[] { o };
    })
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Não usar AllowCredentials() aqui: o front usa Bearer no header, não cookies.
        // AllowCredentials + política dinâmica costuma omitir Access-Control-Allow-Origin em alguns cenários.
        policy.AllowAnyHeader().AllowAnyMethod();
        // Sempre: loopback em qualquer porta (Vite, API em Production local, etc.) + origens explícitas em produção.
        var allowedExact = new HashSet<string>(corsOriginsExpanded, StringComparer.OrdinalIgnoreCase);
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                if (uri.Scheme is not ("http" or "https")) return false;
                if (uri.Host is "localhost" or "127.0.0.1") return true;
                return allowedExact.Contains(origin.TrimEnd('/'));
            }
            catch (UriFormatException)
            {
                return false;
            }
        });
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KYX DocEngine API",
        Version = "v1",
        Description = "Serviço de geração de documentos PDF"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Traefik / reverse proxy: respeitar HTTPS e host públicos (X-Forwarded-*).
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    ForwardLimit = 2
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// Migrações automáticas (templates, document_jobs, tb_log_requisicao). Desligado por defeito — ver Database:ApplyMigrationsOnStartup.
var applyMigrations = builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false);
if (applyMigrations)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocEngineDbContext>();
        db.Database.Migrate();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        DemoTemplateSeeder.TrySeed(db, app.Environment, startupLogger);
    }
    catch (Exception ex)
    {
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        log.LogWarning(
            ex,
            "Não foi possível aplicar migrações EF Core no startup. A API continuará. " +
            "Em PostgreSQL gerenciado, conceda CREATE no schema (ex.: GRANT CREATE ON SCHEMA public TO seu_usuario) " +
            "ou rode 'dotnet ef database update' com um usuário administrador. " +
            "Enquanto as tabelas não existirem, o middleware de tb_log_requisicao falhará ao gravar (sem derrubar as requisições).");
    }
}
else
{
    var logSkip = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logSkip.LogInformation(
        "Migrações no arranque desativadas (Database:ApplyMigrationsOnStartup=false). " +
        "Nenhuma tabela será criada/alterada por este processo. " +
        "Garanta o schema com «dotnet ef database update» ou scripts DBA, ou ative a opção em appsettings.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ordem: Routing → CORS → Auth (CORS antes de Auth evita 401 sem headers de origem)
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});

app.MapControllers();

// Em Docker use ASPNETCORE_URLS=http://+:3000 (localhost dentro do container não recebe tráfego do host).
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = builder.Configuration.GetValue<int>("Port", 3000);
    app.Urls.Add($"http://localhost:{port}");
}

app.Run();

