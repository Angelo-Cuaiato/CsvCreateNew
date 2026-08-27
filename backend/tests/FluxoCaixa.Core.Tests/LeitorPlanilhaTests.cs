using System.Text;
using FluxoCaixa.Core.Leitura;

namespace FluxoCaixa.Core.Tests;

public class LeitorPlanilhaTests
{
    [Fact]
    public void Ler_CabecalhoEValores()
    {
        var planilha = PlanilhaExemplo.DoTexto(PlanilhaExemplo.CsvExemplo);

        Assert.Equal(["JAN/2026", "FEV/2026"], planilha.Meses);
        Assert.Equal(2, planilha.Linhas.Count);

        var recebimentos = planilha.Linha("Total de Recebimentos");
        Assert.NotNull(recebimentos);
        Assert.Equal([100.00m, 200.00m], recebimentos.Previsto);
        Assert.Equal([90.00m, 250.00m], recebimentos.Realizado);
        Assert.Equal(300.00m, recebimentos.TotalPrevistoOrigem);
        Assert.Equal(340.00m, recebimentos.TotalRealizadoOrigem);
    }

    [Fact]
    public void Ler_BuscaDeLinhaIgnoraAcento()
    {
        var planilha = PlanilhaExemplo.DoTexto(PlanilhaExemplo.CsvExemplo);

        Assert.NotNull(planilha.Linha("total de recebimentos"));
        Assert.Null(planilha.Linha("Nao existe"));
    }

    [Fact]
    public void Ler_AvisaQuandoALinhaTemMenosColunas()
    {
        var planilha = PlanilhaExemplo.DoTexto(PlanilhaExemplo.CsvExemplo + "Outra;10,00\r\n");

        Assert.Contains(planilha.Avisos, aviso => aviso.Contains("Outra"));

        var outra = planilha.Linha("Outra");
        Assert.NotNull(outra);
        Assert.Equal(10.00m, outra.Previsto[0]);
        Assert.Null(outra.Previsto[1]);
    }

    [Fact]
    public void Ler_ArquivoSemDadosFalhaComMensagem()
    {
        var erro = Assert.Throws<PlanilhaInvalidaException>(
            () => PlanilhaExemplo.DoTexto("FLUXO DE CAIXA;Previsto (R$)\r\n"));

        Assert.Contains("cabeçalho", erro.Message);
    }

    [Fact]
    public void Ler_DetectaSeparadorVirgula()
    {
        var comVirgula = PlanilhaExemplo.CsvExemplo.Replace(';', ',');
        var planilha = PlanilhaExemplo.DoTexto(comVirgula);

        Assert.Equal(",", planilha.Separador);
        Assert.Equal(["JAN/2026", "FEV/2026"], planilha.Meses);
    }

    [Fact]
    public void Ler_ReconheceWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var conteudo = PlanilhaExemplo.CsvExemplo.Replace("Vendas", "Saldo do Mês Anterior");
        var planilha = PlanilhaExemplo.DoTexto(conteudo, encoding: Encoding.GetEncoding(1252));

        Assert.Equal("windows-1252", planilha.Encoding);
        Assert.NotNull(planilha.Linha("Saldo do Mês Anterior"));
    }

    [Fact]
    public void Ler_PlanilhaReal()
    {
        Assert.True(PlanilhaExemplo.Existe, $"planilha de exemplo ausente em {PlanilhaExemplo.Caminho}");

        var planilha = PlanilhaExemplo.Ler();

        Assert.Equal(";", planilha.Separador);
        Assert.Equal("windows-1252", planilha.Encoding);
        Assert.Equal(12, planilha.Meses.Count);
        Assert.Equal("JAN/2026", planilha.Meses[0]);
        Assert.Equal("DEZ/2026", planilha.Meses[^1]);
        Assert.Equal(59, planilha.Linhas.Count);
        Assert.Empty(planilha.Avisos);
        Assert.NotNull(planilha.Linha("Salários - Médicos"));
    }
}
