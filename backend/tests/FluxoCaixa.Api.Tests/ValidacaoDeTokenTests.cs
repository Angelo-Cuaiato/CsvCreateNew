using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// O que a API aceita como token: prazo, assinatura, emissor e audiência.
/// </summary>
/// <remarks>
/// Os tokens aqui são montados no próprio teste (e não pedidos ao
/// <c>/api/auth/login</c>) porque só assim dá para colocar um vencimento no
/// passado sem deixar o teste esperando.
/// </remarks>
public class ValidacaoDeTokenTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
{
    private const string Emissor = "fluxo-caixa-api";
    private const string Audiencia = "fluxo-caixa-web";

    private static string Token(
        TimeSpan vencimento,
        string chave = ApiDeTeste.Chave,
        string emissor = Emissor,
        string audiencia = Audiencia)
    {
        var expiraEm = DateTime.UtcNow.Add(vencimento);

        var token = new JwtSecurityToken(
            issuer: emissor,
            audience: audiencia,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, ApiDeTeste.Email),
                new Claim(ClaimTypes.Email, ApiDeTeste.Email),
                new Claim(ClaimTypes.Name, "Usuário de Teste"),
            ],
            notBefore: expiraEm.AddHours(-1),
            expires: expiraEm,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient Cliente(string token)
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [Fact]
    public async Task TokenDentroDoPrazo_EAceito()
    {
        // Prova que o token montado aqui é válido: as recusas dos outros casos
        // são pelo motivo testado, não por um token malformado.
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(5))).GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenVencido_ERecusado()
    {
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(-5))).GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);

        // O cabeçalho diz o motivo: é o prazo, não a assinatura.
        var motivo = resposta.Headers.WwwAuthenticate.ToString();
        Assert.Contains("invalid_token", motivo);
        Assert.Contains("expired", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TokenVencidoPorSegundos_ERecusado()
    {
        // Com a folga padrão de 5 minutos do JwtBearer este token passaria.
        // Como ClockSkew está zerado, ele tem que ser recusado.
        var resposta = await Cliente(Token(TimeSpan.FromSeconds(-30))).GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("expired", resposta.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TokenVencido_TambemNaoAbreAsRotasDeFluxo()
    {
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(-1)))
            .PostAsync("/api/fluxo/analisar", ApiDeTeste.Planilha());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenAssinadoComOutraChave_ERecusado()
    {
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(5), chave: "outra-chave-de-teste-com-mais-de-32-bytes"))
            .GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenDeOutroEmissor_ERecusado()
    {
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(5), emissor: "outra-api")).GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenParaOutraAudiencia_ERecusado()
    {
        var resposta = await Cliente(Token(TimeSpan.FromMinutes(5), audiencia: "outro-app")).GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
