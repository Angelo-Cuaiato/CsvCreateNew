using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluxoCaixa.Core.Seguranca;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// A gestão de usuários é a primeira coisa que o perfil realmente tranca:
/// administrador cria, troca senha e exclui; usuário comum não faz nenhuma
/// das três — mas continua vendo todas as análises.
/// </summary>
public class UsuariosTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
{
    private static Task<HttpClient> Administrador(ApiDeTeste api) => api.ClienteDeAdministradorAsync();

    private static Task<HttpClient> Logado(ApiDeTeste api, string email, string senha)
        => api.ClienteLogadoAsync(email, senha);

    /// <summary>Cria um usuário comum e devolve um cliente logado como ele.</summary>
    private static async Task<(HttpClient Cliente, string Email)> UsuarioComum(ApiDeTeste api)
    {
        var admin = await Administrador(api);
        var email = $"comum-{Guid.NewGuid():N}@exemplo.com";
        const string senha = "senha-de-usuario-comum";

        var criacao = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { email, nome = "Comum", senha, perfil = Perfis.Usuario });

        criacao.EnsureSuccessStatusCode();
        return (await Logado(api, email, senha), email);
    }

    [Fact]
    public async Task Sem_Token_Nao_Ve_Usuarios()
    {
        var resposta = await api.CreateClient().GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Administrador_Cria_E_O_Novo_Consegue_Entrar()
    {
        var admin = await Administrador(api);
        var email = $"maria-{Guid.NewGuid():N}@exemplo.com";

        var criacao = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { email, nome = "Maria", senha = "senha-forte-da-maria", perfil = Perfis.Usuario });

        Assert.Equal(HttpStatusCode.OK, criacao.StatusCode);

        var login = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new { email, senha = "senha-forte-da-maria" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task A_lista_nunca_traz_o_hash_da_senha()
    {
        var admin = await Administrador(api);
        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/usuarios");

        foreach (var item in lista.GetProperty("itens").EnumerateArray())
        {
            Assert.False(item.TryGetProperty("senhaHash", out _));
            Assert.DoesNotContain("pbkdf2", item.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("sem-arroba", "Nome", "senha-boa-o-suficiente", Perfis.Usuario, "e-mail")]
    [InlineData("a@b.com", "", "senha-boa-o-suficiente", Perfis.Usuario, "nome")]
    [InlineData("a@b.com", "Nome", "curta", Perfis.Usuario, "8 caracteres")]
    [InlineData("a@b.com", "Nome", "senha-boa-o-suficiente", "chefe", "Perfil inválido")]
    public async Task Recusa_cadastro_mal_preenchido(
        string email, string nome, string senha, string perfil, string trecho)
    {
        var admin = await Administrador(api);
        var resposta = await admin.PostAsJsonAsync("/api/usuarios", new { email, nome, senha, perfil });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(trecho, corpo.GetProperty("mensagem").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Email_repetido_da_conflito_em_vez_de_erro()
    {
        var admin = await Administrador(api);
        var email = $"repetido-{Guid.NewGuid():N}@exemplo.com";
        var corpo = new { email, nome = "Alguém", senha = "senha-boa-o-suficiente", perfil = Perfis.Usuario };

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/usuarios", corpo)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/usuarios", corpo)).StatusCode);
    }

    [Fact]
    public async Task Administrador_troca_a_senha_e_a_antiga_para_de_valer()
    {
        var admin = await Administrador(api);
        var (_, email) = await UsuarioComum(api);

        var troca = await admin.PutAsJsonAsync($"/api/usuarios/{email}/senha", new { senha = "senha-nova-do-usuario" });
        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);

        var comAntiga = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, senha = "senha-de-usuario-comum" });
        var comNova = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, senha = "senha-nova-do-usuario" });

        Assert.Equal(HttpStatusCode.Unauthorized, comAntiga.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comNova.StatusCode);
    }

    [Fact]
    public async Task Administrador_exclui_e_o_excluido_nao_entra_mais()
    {
        var admin = await Administrador(api);
        var (_, email) = await UsuarioComum(api);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/usuarios/{email}")).StatusCode);

        var login = await api.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email, senha = "senha-de-usuario-comum" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Usuario_comum_nao_cria_nao_troca_senha_e_nao_exclui()
    {
        var (comum, _) = await UsuarioComum(api);

        var criar = await comum.PostAsJsonAsync(
            "/api/usuarios",
            new { email = "outro@exemplo.com", nome = "Outro", senha = "senha-boa-o-suficiente", perfil = Perfis.Usuario });

        var trocar = await comum.PutAsJsonAsync(
            $"/api/usuarios/{ApiDeTeste.Email}/senha", new { senha = "senha-boa-o-suficiente" });

        var excluir = await comum.DeleteAsync($"/api/usuarios/{ApiDeTeste.Email}");
        var listar = await comum.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, criar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, trocar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, excluir.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listar.StatusCode);
    }

    [Fact]
    public async Task Usuario_comum_continua_vendo_todos_os_csv_enviados()
    {
        var admin = await Administrador(api);
        var enviado = await admin.PostAsync("/api/fluxo/analisar", ApiDeTeste.Planilha());
        enviado.EnsureSuccessStatusCode();

        var id = (await enviado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (comum, _) = await UsuarioComum(api);
        var historico = await comum.GetFromJsonAsync<JsonElement>("/api/fluxo/historico");

        // O histórico é do escritório, não de cada um: quem entra vê tudo.
        Assert.Contains(
            historico.GetProperty("itens").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.OK, (await comum.GetAsync($"/api/fluxo/historico/{id}/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await comum.GetAsync("/api/fluxo/totais")).StatusCode);
    }

    [Fact]
    public async Task Nao_da_para_excluir_a_si_mesmo()
    {
        var admin = await Administrador(api);
        var resposta = await admin.DeleteAsync($"/api/usuarios/{ApiDeTeste.Email}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("próprio usuário", corpo.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task Nao_da_para_excluir_o_ultimo_administrador()
    {
        // API só deste teste: ele apaga administradores, e isso não pode
        // respingar nos outros, que compartilham a instância da classe.
        using var isolada = new ApiDeTeste();
        var admin = await isolada.ClienteDeAdministradorAsync();

        var outro = "admin2@exemplo.com";
        var criacao = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { email = outro, nome = "Outro Admin", senha = "senha-do-outro-admin", perfil = Perfis.Administrador });

        criacao.EnsureSuccessStatusCode();
        var comOutro = await isolada.ClienteLogadoAsync(outro, "senha-do-outro-admin");

        // Com dois administradores, dá para excluir um.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await comOutro.DeleteAsync($"/api/usuarios/{ApiDeTeste.Email}")).StatusCode);

        // Sobrou um, e ele é quem está pedindo: as duas travas valem.
        var sozinho = await comOutro.DeleteAsync($"/api/usuarios/{outro}");
        Assert.Equal(HttpStatusCode.BadRequest, sozinho.StatusCode);

        var corpo = await sozinho.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("próprio usuário", corpo.GetProperty("mensagem").GetString());
    }
}
