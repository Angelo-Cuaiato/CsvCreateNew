using System.Net.Http.Json;
using FluxoCaixa.Core.Seguranca;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// Sobe a API em memória com um usuário e uma chave de teste, sem depender do
/// appsettings do ambiente.
/// </summary>
public sealed class ApiDeTeste : WebApplicationFactory<Program>
{
    public const string Email = "teste@exemplo.com";
    public const string Senha = "Fluxo@2026";
    public const string Chave = "chave-de-teste-com-mais-de-32-bytes-para-o-hmac";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureHostConfiguration(configuracao => configuracao.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:ChaveSecreta"] = Chave,
                ["Jwt:MinutosDeValidade"] = "60",
                ["Usuarios:0:Email"] = Email,
                ["Usuarios:0:Nome"] = "Usuário de Teste",
                ["Usuarios:0:Perfil"] = Perfis.Administrador,
                ["Usuarios:0:SenhaHash"] = HashDeSenha.Gerar(Senha),
            }));

        return base.CreateHost(builder);
    }

    private string? _tokenDeAdministrador;

    /// <summary>
    /// Um cliente já autenticado como o administrador de teste.
    /// </summary>
    /// <remarks>
    /// O token é reaproveitado de propósito: o login aceita 10 tentativas por
    /// minuto por IP, e uma classe de teste que entra a cada método esbarra
    /// nesse limite - que existe justamente para isso.
    /// </remarks>
    public async Task<HttpClient> ClienteDeAdministradorAsync()
    {
        _tokenDeAdministrador ??= await EntrarAsync(Email, Senha);

        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokenDeAdministrador);

        return cliente;
    }

    /// <summary>Faz login e devolve o token.</summary>
    public async Task<string> EntrarAsync(string email, string senha)
    {
        var resposta = await CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return corpo.GetProperty("token").GetString()!;
    }

    /// <summary>Um cliente autenticado como quem você mandar.</summary>
    public async Task<HttpClient> ClienteLogadoAsync(string email, string senha)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await EntrarAsync(email, senha));

        return cliente;
    }

    /// <summary>A planilha de exemplo do repositório.</summary>
    public static string CaminhoDaPlanilha => Path.Combine(AppContext.BaseDirectory, "Dados", "fluxo_de_caixa_mensal.csv");

    public static MultipartFormDataContent Planilha()
    {
        var conteudo = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(File.ReadAllBytes(CaminhoDaPlanilha));
        conteudo.Add(arquivo, "arquivo", "fluxo_de_caixa_mensal.csv");
        return conteudo;
    }
}
