using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FluxoCaixa.Api.Tests;

public class AutenticacaoTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
{
    private static async Task<string> Entrar(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiDeTeste.Email, senha = ApiDeTeste.Senha });

        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Saude_NaoPedeToken()
    {
        var resposta = await api.CreateClient().GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_DevolveTokenEDadosDoUsuario()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiDeTeste.Email, senha = ApiDeTeste.Senha });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("token").GetString()));
        Assert.Equal(ApiDeTeste.Email, corpo.GetProperty("email").GetString());
        Assert.Equal("Usuário de Teste", corpo.GetProperty("nome").GetString());
        Assert.True(corpo.GetProperty("expiraEm").GetDateTime() > DateTime.UtcNow);

        // O token tem três partes (cabeçalho.corpo.assinatura) e nada da senha.
        var token = corpo.GetProperty("token").GetString()!;
        Assert.Equal(3, token.Split('.').Length);
        Assert.DoesNotContain(ApiDeTeste.Senha, token);
    }

    [Theory]
    [InlineData(ApiDeTeste.Email, "senha-errada")]
    [InlineData("naoexiste@exemplo.com", ApiDeTeste.Senha)]
    [InlineData("", "")]
    public async Task Login_RecusaCredenciaisInvalidas(string email, string senha)
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, senha });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);

        // A mensagem é a mesma nos dois casos: não conta quais e-mails existem.
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("E-mail ou senha inválidos.", corpo.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task Eu_DevolveQuemEstaLogado()
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Entrar(cliente));

        var corpo = await cliente.GetFromJsonAsync<JsonElement>("/api/auth/eu");

        Assert.Equal(ApiDeTeste.Email, corpo.GetProperty("email").GetString());
        Assert.Equal("administrador", corpo.GetProperty("perfil").GetString());
    }

    [Theory]
    [InlineData("/api/fluxo/analisar")]
    [InlineData("/api/fluxo/consolidar")]
    public async Task RotasDeFluxo_ExigemToken(string rota)
    {
        var resposta = await api.CreateClient().PostAsync(rota, ApiDeTeste.Planilha());

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Eu_RecusaTokenAdulterado()
    {
        var cliente = api.CreateClient();
        var token = await Entrar(cliente);
        var adulterado = token[..^2] + (token.EndsWith("aa") ? "bb" : "aa");

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adulterado);
        var resposta = await cliente.GetAsync("/api/auth/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Analisar_FuncionaComToken()
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Entrar(cliente));

        var resposta = await cliente.PostAsync("/api/fluxo/analisar", ApiDeTeste.Planilha());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12, corpo.GetProperty("meses").GetArrayLength());
        Assert.True(corpo.GetProperty("conferencia").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Consolidar_DevolveOCsvComToken()
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Entrar(cliente));

        var resposta = await cliente.PostAsync("/api/fluxo/consolidar", ApiDeTeste.Planilha());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);

        var csv = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("MÊS 01 - JAN/2026", csv);
        Assert.Contains("TOTAL GERAL", csv);
    }
}
