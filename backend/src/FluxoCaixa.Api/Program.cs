using System.Text.Json;
using System.Text.Json.Serialization;
using FluxoCaixa.Core;
using FluxoCaixa.Core.Leitura;
using FluxoCaixa.Core.Relatorio;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

const string PoliticaCors = "front-angular";

builder.Services.AddCors(opcoes => opcoes.AddPolicy(PoliticaCors, politica => politica
    .WithOrigins(
        builder.Configuration.GetSection("Cors:Origens").Get<string[]>()
        ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

builder.Services.AddSingleton<ServicoFluxoCaixa>();

builder.Services.Configure<FormOptions>(opcoes =>
{
    // Planilhas de fluxo de caixa sao pequenas; 20 MB e folga suficiente.
    opcoes.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opcoes.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();

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

app.UseCors(PoliticaCors);

app.MapGet("/api/saude", () => Results.Ok(new { status = "ok" }));

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
.DisableAntiforgery();

app.Run();

/// <summary>Exposta para os testes de integracao da API.</summary>
public partial class Program;
