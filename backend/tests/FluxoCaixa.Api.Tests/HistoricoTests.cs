using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// O histórico existe para uma coisa: voltar a uma análise depois, sem ter a
/// planilha de origem em mãos. Os testes seguem esse caminho.
/// </summary>
public class HistoricoTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
{
    private static async Task<HttpClient> ClienteLogado(ApiDeTeste api)
    {
        var cliente = api.CreateClient();

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiDeTeste.Email, senha = ApiDeTeste.Senha });

        login.EnsureSuccessStatusCode();
        var corpo = await login.Content.ReadFromJsonAsync<JsonElement>();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", corpo.GetProperty("token").GetString());

        return cliente;
    }

    private static async Task<Guid> Analisar(HttpClient cliente)
    {
        var resposta = await cliente.PostAsync("/api/fluxo/analisar", ApiDeTeste.Planilha());
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("id").GetGuid();
    }

    [Theory]
    [InlineData("/api/fluxo/historico")]
    public async Task Historico_ExigeToken(string rota)
    {
        var resposta = await api.CreateClient().GetAsync(rota);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Analisar_Guarda_E_A_Analise_Aparece_Na_Lista()
    {
        var cliente = await ClienteLogado(api);
        var id = await Analisar(cliente);

        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/historico");
        var itens = lista.GetProperty("itens").EnumerateArray().ToList();

        var guardada = itens.Single(item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal("fluxo_de_caixa_mensal.csv", guardada.GetProperty("nomeArquivo").GetString());
        Assert.Equal(ApiDeTeste.Email, guardada.GetProperty("email").GetString());
        Assert.Equal(12, guardada.GetProperty("quantidadeDeMeses").GetInt32());
        Assert.Equal("JAN/2026", guardada.GetProperty("primeiroMes").GetString());
        Assert.Equal("DEZ/2026", guardada.GetProperty("ultimoMes").GetString());
        Assert.True(guardada.GetProperty("conferenciaOk").GetBoolean());
    }

    [Fact]
    public async Task Analise_Guardada_Volta_Inteira()
    {
        var cliente = await ClienteLogado(api);
        var id = await Analisar(cliente);

        var aberta = await cliente.GetFromJsonAsync<JsonElement>($"/api/fluxo/historico/{id}");
        var relatorio = aberta.GetProperty("relatorio");

        Assert.Equal(id, aberta.GetProperty("id").GetGuid());
        Assert.Equal(12, relatorio.GetProperty("meses").GetArrayLength());
        Assert.NotEmpty(relatorio.GetProperty("detalhePorMes").EnumerateArray());
        Assert.NotEmpty(relatorio.GetProperty("totalGeral").EnumerateArray());
        Assert.True(relatorio.GetProperty("conferencia").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Csv_Do_Historico_E_Igual_Ao_Que_A_Consolidacao_Gera()
    {
        var cliente = await ClienteLogado(api);
        var id = await Analisar(cliente);

        var doHistorico = await cliente.GetAsync($"/api/fluxo/historico/{id}/csv");
        Assert.Equal(HttpStatusCode.OK, doHistorico.StatusCode);
        Assert.Equal("text/csv", doHistorico.Content.Headers.ContentType?.MediaType);

        var deAgora = await cliente.PostAsync("/api/fluxo/consolidar", ApiDeTeste.Planilha());
        deAgora.EnsureSuccessStatusCode();

        // O arquivo guardado é o mesmo que a consolidação produz - fora a linha
        // "Gerado em", que carrega o horário.
        Assert.Equal(
            SemOCarimboDeHora(await deAgora.Content.ReadAsStringAsync()),
            SemOCarimboDeHora(await doHistorico.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Csv_Do_Historico_Nao_Precisa_Da_Planilha_De_Origem()
    {
        var cliente = await ClienteLogado(api);
        var id = await Analisar(cliente);

        // Nenhum upload aqui: é este o ponto do histórico.
        var resposta = await cliente.GetAsync($"/api/fluxo/historico/{id}/csv");
        var csv = await resposta.Content.ReadAsStringAsync();

        Assert.Contains("MÊS 01 - JAN/2026", csv);
        Assert.Contains("TOTAL GERAL", csv);
        Assert.Equal(
            "fluxo_de_caixa_mensal_consolidado.csv",
            resposta.Content.Headers.ContentDisposition?.FileNameStar
                ?? resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Analise_Que_Nao_Existe_Da_404()
    {
        var cliente = await ClienteLogado(api);
        var inventado = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/fluxo/historico/{inventado}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/fluxo/historico/{inventado}/csv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.DeleteAsync($"/api/fluxo/historico/{inventado}")).StatusCode);
    }

    [Fact]
    public async Task Apagar_Tira_Do_Historico()
    {
        var cliente = await ClienteLogado(api);
        var id = await Analisar(cliente);

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.DeleteAsync($"/api/fluxo/historico/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/fluxo/historico/{id}")).StatusCode);

        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/historico");
        Assert.DoesNotContain(
            lista.GetProperty("itens").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Sem_Banco_A_Lista_Se_Declara_Nao_Persistente()
    {
        // A API de teste sobe sem PostgreSQL. Prometer um histórico que some no
        // próximo reinício seria pior do que avisar.
        var cliente = await ClienteLogado(api);
        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/historico");

        Assert.False(lista.GetProperty("persistente").GetBoolean());
    }

    private static string SemOCarimboDeHora(string csv) => string.Join(
        '\n',
        csv.Split('\n').Where(linha => !linha.StartsWith("Gerado em", StringComparison.Ordinal)));
}

/// <summary>
/// O somatório: um relatório só, com os valores de todas as análises guardadas
/// somados - e o total geral no final.
/// </summary>
public class SomatorioTests(ApiDeTeste api) : IClassFixture<ApiDeTeste>
{
    private static async Task<HttpClient> ClienteLogado(ApiDeTeste api)
    {
        var cliente = api.CreateClient();

        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { email = ApiDeTeste.Email, senha = ApiDeTeste.Senha });

        login.EnsureSuccessStatusCode();
        var corpo = await login.Content.ReadFromJsonAsync<JsonElement>();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", corpo.GetProperty("token").GetString());

        return cliente;
    }

    private static async Task<Guid> Analisar(HttpClient cliente)
    {
        var resposta = await cliente.PostAsync("/api/fluxo/analisar", ApiDeTeste.Planilha());
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("id").GetGuid();
    }

    private static decimal Realizado(JsonElement relatorio, string rotulo) => relatorio
        .GetProperty("totalGeral")
        .EnumerateArray()
        .First(item => item.GetProperty("rotulo").GetString() == rotulo)
        .GetProperty("realizado")
        .GetDecimal();

    [Fact]
    public async Task Somatorio_Exige_Token()
    {
        var resposta = await api.CreateClient().PostAsJsonAsync("/api/fluxo/somatorio", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Duas_Analises_Somadas_Dobram_O_Total()
    {
        var cliente = await ClienteLogado(api);
        var primeira = await Analisar(cliente);
        var segunda = await Analisar(cliente);

        var uma = await cliente.GetFromJsonAsync<JsonElement>($"/api/fluxo/historico/{primeira}");
        var sozinha = uma.GetProperty("relatorio");

        var resposta = await cliente.PostAsJsonAsync(
            "/api/fluxo/somatorio",
            new { ids = new[] { primeira, segunda } });

        resposta.EnsureSuccessStatusCode();
        var somado = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var rotulo in new[]
        {
            "Total de recebimentos",
            "Total de pagamentos",
            "Geração de caixa do período",
            "Saldo final do período",
        })
        {
            Assert.Equal(Realizado(sozinha, rotulo) * 2, Realizado(somado, rotulo));
        }
    }

    [Fact]
    public async Task Sem_Lista_Soma_Todas_As_Guardadas()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);

        var lista = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/historico");
        var somaveis = lista.GetProperty("itens").EnumerateArray()
            .Where(item => item.GetProperty("podeSomar").GetBoolean())
            .ToList();

        var resposta = await cliente.PostAsJsonAsync("/api/fluxo/somatorio", new { });
        resposta.EnsureSuccessStatusCode();

        var somado = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        // Todas as análises são da mesma planilha, então o total tem que dar
        // exatamente N vezes o de uma - lido daqui, e não fixado no teste.
        var qualquer = await cliente.GetFromJsonAsync<JsonElement>(
            $"/api/fluxo/historico/{somaveis[0].GetProperty("id").GetGuid()}");

        var uma = Realizado(qualquer.GetProperty("relatorio"), "Total de recebimentos");
        Assert.Equal(uma * somaveis.Count, Realizado(somado, "Total de recebimentos"));
    }

    [Fact]
    public async Task O_Csv_Do_Somatorio_Traz_O_Total_Geral_No_Final()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);

        var resposta = await cliente.PostAsJsonAsync("/api/fluxo/somatorio/csv", new { });
        resposta.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);

        var csv = await resposta.Content.ReadAsStringAsync();

        // O arquivo é CRLF: comparar linha inteira esbarraria no \r do fim.
        var linhas = csv.Split('\n').Select(linha => linha.TrimEnd('\r')).ToList();

        var totalGeral = linhas.FindIndex(linha => linha.StartsWith("TOTAL GERAL", StringComparison.Ordinal));
        var resumo = linhas.FindIndex(linha => linha.StartsWith("RESUMO POR MÊS", StringComparison.Ordinal));

        Assert.True(totalGeral > 0, "O somatório precisa ter a seção TOTAL GERAL.");
        Assert.True(totalGeral > resumo, "O TOTAL GERAL fica no final, depois do resumo por mês.");

        // Depois dele só vem a conferência - nenhum outro bloco de números.
        Assert.DoesNotContain(
            linhas.Skip(totalGeral + 1),
            linha => linha.StartsWith("MÊS ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task O_Nome_Das_Planilhas_Somadas_Aparece_No_Relatorio()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);
        await Analisar(cliente);

        var resposta = await cliente.PostAsJsonAsync("/api/fluxo/somatorio", new { });
        var somado = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        var origem = somado.GetProperty("arquivo").GetString() ?? string.Empty;
        Assert.Contains("fluxo_de_caixa_mensal.csv + fluxo_de_caixa_mensal.csv", origem);
    }

    [Fact]
    public async Task Os_Totais_De_Tudo_Vem_Prontos_Sem_Pedir_Somatorio()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);

        var totais = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/totais");

        Assert.True(totais.GetProperty("analises").GetInt32() > 0);
        Assert.Contains("JAN/2026", totais.GetProperty("periodo").GetString());
        Assert.NotEmpty(totais.GetProperty("totalGeral").EnumerateArray());

        // É o fechamento, não os doze meses: isto é buscado a cada envio.
        Assert.False(totais.TryGetProperty("detalhePorMes", out _));
    }

    [Fact]
    public async Task Os_Totais_De_Tudo_Batem_Com_O_Somatorio_Completo()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);

        var totais = await cliente.GetFromJsonAsync<JsonElement>("/api/fluxo/totais");

        var completo = await cliente.PostAsJsonAsync("/api/fluxo/somatorio", new { });
        var somado = await completo.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            Realizado(somado, "Saldo final do período"),
            Realizado(totais, "Saldo final do período"));
    }

    [Fact]
    public async Task Pedir_Somatorio_De_Id_Que_Nao_Existe_Explica_Em_Vez_De_Somar_Tudo()
    {
        var cliente = await ClienteLogado(api);
        await Analisar(cliente);

        var resposta = await cliente.PostAsJsonAsync(
            "/api/fluxo/somatorio",
            new { ids = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Não há análises guardadas", corpo.GetProperty("mensagem").GetString());
    }
}
