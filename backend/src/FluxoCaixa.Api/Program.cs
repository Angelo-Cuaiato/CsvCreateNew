using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluxoCaixa.Api.Seguranca;
using FluxoCaixa.Core;
using FluxoCaixa.Core.Leitura;
using FluxoCaixa.Core.Relatorio;
using FluxoCaixa.Core.Seguranca;
using FluxoCaixa.Dados;
using Npgsql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

// Utilitário de linha de comando para cadastrar senhas:
//     dotnet run --project src/FluxoCaixa.Api -- hash-senha "minha senha"
if (args is ["hash-senha", var senhaParaHash])
{
    Console.WriteLine(HashDeSenha.Gerar(senhaParaHash));
    return 0;
}

// Sonda usada pelo HEALTHCHECK do contêiner. Fica aqui, e não num curl, para a
// imagem não depender de instalar nada além do runtime.
if (args is ["--saude"])
{
    var endereco = Environment.GetEnvironmentVariable("SAUDE_URL") ?? "http://localhost:8080/api/saude";
    using var sonda = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

    try
    {
        var resposta = await sonda.GetAsync(endereco);
        return resposta.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception)
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Sem "Server: Kestrel" nas respostas: a API não anuncia com o que foi feita.
builder.WebHost.ConfigureKestrel(opcoes => opcoes.AddServerHeader = false);

const string PoliticaCors = "front-angular";
const string LimiteDeLogin = "login";
const string LimiteDeFluxo = "fluxo";

builder.Services.AddCors(opcoes => opcoes.AddPolicy(PoliticaCors, politica => politica
    .WithOrigins(
        builder.Configuration.GetSection("Cors:Origens").Get<string[]>()
        ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddSingleton<ServicoFluxoCaixa>();

// ------------------------------------------------------------------ segurança

builder.Services.Configure<OpcoesJwt>(builder.Configuration.GetSection(OpcoesJwt.Secao));
builder.Services.AddSingleton<GeradorDeToken>();

var opcoesJwt = builder.Configuration.GetSection(OpcoesJwt.Secao).Get<OpcoesJwt>() ?? new OpcoesJwt();
opcoesJwt.Validar(producao: builder.Environment.IsProduction());

// A senha do usuário inicial vem em texto puro pela configuração; a de exemplo
// não pode virar a senha do administrador de produção.
if (builder.Environment.IsProduction()
    && OpcoesJwt.EhDeExemplo(builder.Configuration["UsuarioInicial:Senha"]))
{
    throw new InvalidOperationException(
        "UsuarioInicial:Senha ainda é a senha de exemplo do repositório. Troque em ADMIN_SENHA.");
}

// Com banco configurado os usuários vêm do PostgreSQL; sem ele, da própria
// configuração (é assim que os testes e o `dotnet run` local funcionam).
var conexaoPostgres = builder.Configuration.GetConnectionString("Postgres");
var usandoBanco = !string.IsNullOrWhiteSpace(conexaoPostgres);

if (usandoBanco)
{
    // Aceita tanto a URI que as plataformas oferecem pronta quanto a string de
    // palavras-chave do compose; reclama com o motivo se não for nenhuma das duas.
    builder.Services.AddSingleton(NpgsqlDataSource.Create(ConexaoPostgres.Normalizar(conexaoPostgres)));
    builder.Services.AddSingleton<RepositorioUsuariosPostgres>();
    builder.Services.AddSingleton<IRepositorioUsuarios>(s => s.GetRequiredService<RepositorioUsuariosPostgres>());
}
else
{
    builder.Services.AddSingleton<IRepositorioUsuarios>(_ => new RepositorioUsuariosEmMemoria(
        builder.Configuration.GetSection("Usuarios").Get<List<UsuarioConfigurado>>()?.Select(u => u.ParaUsuario())
        ?? []));
}

builder.Services.AddSingleton<ServicoAutenticacao>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcoesJwt.Emissor,
            ValidateAudience = true,
            ValidAudience = opcoesJwt.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = opcoesJwt.Chave(),
            ValidateLifetime = true,
            // Sem a folga padrão de 5 minutos: o token expira na hora marcada.
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// Login é a porta de entrada: segura a força bruta antes de chegar no hash.
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy(LimiteDeLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));

    // Cada chamada de fluxo lê uma planilha inteira em memória (até 20 MB), o
    // que a torna o caminho barato para derrubar a API por consumo. O balde é
    // por usuário autenticado - e não por IP - para que um escritório inteiro
    // atrás do mesmo IP não divida a cota.
    opcoes.AddPolicy(LimiteDeFluxo, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.User.FindFirstValue(ClaimTypes.Email)
        ?? contexto.Connection.RemoteIpAddress?.ToString()
        ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            // Uma rajada curta espera em vez de levar 429 na cara.
            QueueLimit = 5,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));
});

// Atrás de proxy (Anubis + nginx) o IP que chega no Kestrel é o do proxy.
// Sem ler o X-Forwarded-For, o limite de tentativas de login contaria todos os
// usuários no mesmo balde. Fica desligado por padrão de propósito: confiar
// nesse cabeçalho quando a API está exposta direto deixaria qualquer cliente
// forjar o próprio IP e escapar do limite.
var atrasDeProxy = builder.Configuration.GetValue<bool>("AtrasDeProxy");

if (atrasDeProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(opcoes =>
    {
        opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Na rede do compose só o proxy alcança a API, então o cabeçalho que
        // chega aqui é confiável.
        opcoes.KnownNetworks.Clear();
        opcoes.KnownProxies.Clear();
    });
}

builder.Services.Configure<FormOptions>(opcoes =>
{
    // Planilhas de fluxo de caixa são pequenas; 20 MB é folga suficiente.
    opcoes.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opcoes.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();

if (usandoBanco)
{
    var registro = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Banco");
    var fonte = app.Services.GetRequiredService<NpgsqlDataSource>();
    var repositorio = app.Services.GetRequiredService<RepositorioUsuariosPostgres>();

    await EsquemaDoBanco.PrepararAsync(fonte, registro);
    await CargaInicial.AplicarAsync(
        repositorio,
        builder.Configuration.GetSection("UsuarioInicial").Get<UsuarioInicial>() ?? new UsuarioInicial(null, null),
        registro);
}
else
{
    // Sem banco a API sobe e atende, mas os usuários vêm da configuração - que
    // em produção está vazia. O sintoma é um 401 em todo login, sem nada no log
    // explicando por quê. Então diga, alto, em qual dos dois modos ela subiu.
    var registro = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Banco");
    var cadastrados = (RepositorioUsuariosEmMemoria)app.Services.GetRequiredService<IRepositorioUsuarios>();

    registro.LogWarning(
        "Subindo SEM banco de dados: ConnectionStrings__Postgres não chegou nesta instância. "
        + "Os usuários vêm da configuração, e há {Quantidade} cadastrado(s) - com zero, "
        + "todo login responde 401. Se você esperava usar o PostgreSQL, confira se a variável "
        + "está salva NESTE serviço e se o deploy aconteceu depois de salvar.",
        cadastrados.Quantidade);
}

// Erros de leitura viram 400 com uma mensagem que o front pode mostrar direto.
app.UseExceptionHandler(rota => rota.Run(async contexto =>
{
    var erro = contexto.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

    var (status, mensagem) = erro switch
    {
        PlanilhaInvalidaException invalida => (StatusCodes.Status400BadRequest, invalida.Message),
        _ => (StatusCodes.Status500InternalServerError, "Não foi possível processar a planilha."),
    };

    contexto.Response.StatusCode = status;
    contexto.Response.ContentType = "application/json; charset=utf-8";
    await contexto.Response.WriteAsync(JsonSerializer.Serialize(new { mensagem }));
}));

if (atrasDeProxy)
{
    // Antes do limitador: é ele quem depende do IP de origem.
    app.UseForwardedHeaders();
}

app.UseCors(PoliticaCors);

// A autenticação vem antes do limitador de propósito: é ela que preenche o
// usuário, e o limite das rotas de fluxo é por usuário, não por IP.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// ------------------------------------------------------------------ endpoints

app.MapGet("/api/saude", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/auth/login", (
    [FromBody] PedidoDeLogin pedido,
    [FromServices] ServicoAutenticacao autenticacao,
    [FromServices] GeradorDeToken gerador) =>
{
    var usuario = autenticacao.Autenticar(pedido.Email, pedido.Senha);

    // Mesma resposta para e-mail inexistente e senha errada.
    return usuario is null
        ? Results.Json(new { mensagem = "E-mail ou senha inválidos." }, statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(gerador.Emitir(usuario));
})
.WithName("Login")
.RequireRateLimiting(LimiteDeLogin);

app.MapGet("/api/auth/eu", (ClaimsPrincipal quem) => Results.Ok(new
{
    email = quem.FindFirstValue(ClaimTypes.Email) ?? quem.Identity?.Name,
    nome = quem.FindFirstValue(ClaimTypes.Name),
    perfil = quem.FindFirstValue(ClaimTypes.Role),
}))
.WithName("QuemSouEu")
.RequireAuthorization();

app.MapPost("/api/fluxo/analisar", (
    [FromForm] IFormFile arquivo,
    [FromServices] ServicoFluxoCaixa servico,
    [FromQuery] bool? incluirZerados) =>
{
    var opcoes = OpcoesRelatorio.Padrao with { IncluirZerados = incluirZerados ?? false };

    using var conteudo = arquivo.OpenReadStream();
    var relatorio = servico.Analisar(conteudo, arquivo.FileName, opcoes);
    return Results.Ok(relatorio);
})
.WithName("AnalisarFluxo")
.RequireAuthorization()
.RequireRateLimiting(LimiteDeFluxo)
.DisableAntiforgery();

app.MapPost("/api/fluxo/consolidar", (
    [FromForm] IFormFile arquivo,
    [FromServices] ServicoFluxoCaixa servico,
    [FromQuery] bool? incluirZerados,
    [FromQuery] bool? semRecuo,
    [FromQuery] string? separador) =>
{
    var opcoes = OpcoesRelatorio.Padrao with
    {
        IncluirZerados = incluirZerados ?? false,
        Recuar = !(semRecuo ?? false),
        Separador = string.IsNullOrEmpty(separador) ? ";" : separador,
    };

    using var conteudo = arquivo.OpenReadStream();
    var csv = servico.Consolidar(conteudo, arquivo.FileName, opcoes);
    return Results.File(csv, "text/csv; charset=utf-8", ServicoFluxoCaixa.NomeSugerido(arquivo.FileName));
})
.WithName("ConsolidarFluxo")
.RequireAuthorization()
.RequireRateLimiting(LimiteDeFluxo)
.DisableAntiforgery();

app.Run();

return 0;

/// <summary>Credenciais enviadas pelo front.</summary>
public sealed record PedidoDeLogin(string? Email, string? Senha);

/// <summary>Usuário como ele aparece na configuração.</summary>
public sealed class UsuarioConfigurado
{
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = Perfis.Usuario;

    public Usuario ParaUsuario() => new(Email, Nome, SenhaHash, Perfil);
}

/// <summary>Exposta para os testes de integração da API.</summary>
public partial class Program;
