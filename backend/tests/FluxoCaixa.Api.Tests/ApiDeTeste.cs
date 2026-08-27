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
