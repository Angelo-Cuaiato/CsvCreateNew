using FluxoCaixa.Core.Leitura;
using FluxoCaixa.Core.Relatorio;

namespace FluxoCaixa.Core.Tests;

/// <summary>
/// O arquivo enviado é mesmo um fluxo de caixa?
/// </summary>
public class ReconhecedorDeMesTests
{
    [Theory]
    [InlineData("JAN/2026")]
    [InlineData("jan/26")]
    [InlineData("DEZ-2026")]
    [InlineData("Janeiro/2026")]
    [InlineData("MARÇO/2026")]
    [InlineData("01/2026")]
    [InlineData("2026-01")]
    [InlineData("SET")]
    public void Reconhece_OsFormatosDeMesUsados(string rotulo)
        => Assert.True(ReconhecedorDeMes.Parece(rotulo), rotulo);

    [Theory]
    [InlineData("Caneta")]
    [InlineData("3.50")]
    [InlineData("120")]
    [InlineData("produto")]
    [InlineData("Total")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("13/2026")]
    public void NaoConfundeComOutraCoisa(string? rotulo)
        => Assert.False(ReconhecedorDeMes.Parece(rotulo), rotulo ?? "(null)");
}

public class ArquivoQueNaoEFluxoDeCaixaTests
{
    private const string ListaDeProdutos =
        "id,produto,preco,estoque\r\n" +
        "1,Caneta,3.50,120\r\n" +
        "2,Caderno,18.90,45\r\n" +
        "3,Mochila,159.00,8\r\n";

    [Fact]
    public void ListaDeProdutos_ERecusadaComMensagemUtil()
    {
        // Antes desta validação o arquivo passava: virava um relatório vazio
        // com a conferência dizendo que estava tudo certo.
        var erro = Assert.Throws<PlanilhaInvalidaException>(
            () => PlanilhaExemplo.DoTexto(ListaDeProdutos));

        Assert.Contains("não parece uma planilha de fluxo de caixa", erro.Message);
        Assert.Contains("Caneta", erro.Message);
    }

    [Fact]
    public void PlanilhaDeVerdade_ContinuaPassando()
    {
        var planilha = PlanilhaExemplo.Ler();
        Assert.Equal(12, planilha.Meses.Count);
    }

    [Fact]
    public void ColunasExtrasNaoAtrapalham()
    {
        // Metade basta: planilhas reais trazem Média, Acumulado e afins.
        var comExtras =
            "FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$)\r\n" +
            "CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026;Média;Média\r\n" +
            "Total de Recebimentos;100,00;90,00;200,00;250,00;150,00;170,00\r\n";

        var planilha = PlanilhaExemplo.DoTexto(comExtras);
        Assert.Equal(["JAN/2026", "FEV/2026", "Média"], planilha.Meses);
    }
}

public class ConferenciaSemColunaTotalTests
{
    private const string SemTotal =
        "FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$)\r\n" +
        "CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026\r\n" +
        "Total de Recebimentos;100,00;90,00;200,00;250,00\r\n" +
        "Vendas;100,00;90,00;200,00;250,00\r\n";

    [Fact]
    public void SemColunaTotal_NaoDizQueConfere()
    {
        var dto = new GeradorRelatorio(PlanilhaExemplo.DoTexto(SemTotal)).MontarDto();

        Assert.False(dto.Conferencia.Comparavel);
        Assert.False(dto.Conferencia.Ok);          // "ok" sem comparação seria enganoso
        Assert.Empty(dto.Conferencia.Divergencias);
    }

    [Fact]
    public void SemColunaTotal_OCsvExplicaOMotivo()
    {
        var linhas = new GeradorRelatorio(PlanilhaExemplo.DoTexto(SemTotal)).MontarCsv();
        var indice = linhas.ToList().FindIndex(l => l.Length > 0 && l[0] == "CONFERÊNCIA");

        Assert.Contains("não trazia coluna Total", linhas[indice + 1][0]);
    }

    [Fact]
    public void ComColunaTotal_ConfereNormalmente()
    {
        var dto = new GeradorRelatorio(PlanilhaExemplo.Ler()).MontarDto();

        Assert.True(dto.Conferencia.Comparavel);
        Assert.True(dto.Conferencia.Ok);
    }
}
