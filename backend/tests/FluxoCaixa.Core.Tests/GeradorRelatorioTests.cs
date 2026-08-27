using System.Text;
using FluxoCaixa.Core;
using FluxoCaixa.Core.Relatorio;

namespace FluxoCaixa.Core.Tests;

public class GeradorRelatorioTests
{
    private readonly IReadOnlyList<string[]> _linhas;
    private readonly string[] _primeiras;
    private readonly RelatorioDto _dto;

    public GeradorRelatorioTests()
    {
        var gerador = new GeradorRelatorio(PlanilhaExemplo.Ler());
        _linhas = gerador.MontarCsv();
        _primeiras = _linhas.Select(linha => linha.Length > 0 ? linha[0] : string.Empty).ToArray();
        _dto = gerador.MontarDto();
    }

    [Fact]
    public void TemUmBlocoPorMes()
    {
        var blocos = _primeiras.Where(nome => nome.StartsWith("MÊS ")).ToArray();

        Assert.Equal(12, blocos.Length);
        Assert.Equal("MÊS 01 - JAN/2026", blocos[0]);
        Assert.Equal("MÊS 12 - DEZ/2026", blocos[^1]);
    }

    [Fact]
    public void SecoesNaOrdemEsperada()
    {
        string[] esperadas =
        [
            "RESUMO POR MÊS",
            "MÊS 01 - JAN/2026",
            "TOTAL DO PERÍODO - JAN/2026 A DEZ/2026",
            "TOTAL GERAL",
            "CONFERÊNCIA",
        ];

        Assert.Equal(esperadas, _primeiras.Where(esperadas.Contains).ToArray());
    }

    [Fact]
    public void TotalGeralFicaNoFim()
    {
        var posicaoTotal = Array.IndexOf(_primeiras, "TOTAL GERAL");
        var ultimoMes = Array.FindLastIndex(_primeiras, nome => nome.StartsWith("MÊS "));

        Assert.True(posicaoTotal > ultimoMes);
    }

    [Fact]
    public void TotalGeralConfereComAOrigem()
    {
        var planilha = PlanilhaExemplo.Ler();
        (string Rotulo, string Origem)[] itens =
        [
            ("Total de recebimentos", "Total de Recebimentos"),
            ("Total de pagamentos", "Total de Pagamentos"),
            ("Geração de caixa do período", "Geração de Caixa do Período"),
        ];

        foreach (var (rotulo, origem) in itens)
        {
            var gerado = _dto.TotalGeral.Single(categoria => categoria.Rotulo == rotulo);
            var linha = planilha.Linha(origem);

            Assert.NotNull(linha);
            Assert.Equal(linha.TotalPrevistoOrigem, gerado.Previsto);
            Assert.Equal(linha.TotalRealizadoOrigem, gerado.Realizado);
        }
    }

    [Fact]
    public void SaldosUsamOPrimeiroEOUltimoMesEmVezDaSoma()
    {
        var planilha = PlanilhaExemplo.Ler();
        var saldoInicial = planilha.Linha("Saldo do Mês Anterior")!;
        var saldoFinal = planilha.Linha("Saldo Final de Caixa")!;

        Assert.Equal(
            saldoInicial.Realizado[0],
            _dto.TotalGeral.Single(c => c.Rotulo == "Saldo inicial do período").Realizado);

        Assert.Equal(
            saldoFinal.Realizado[^1],
            _dto.TotalGeral.Single(c => c.Rotulo == "Saldo final do período").Realizado);
    }

    [Fact]
    public void ConferenciaSemDivergencias()
    {
        Assert.True(_dto.Conferencia.Ok);
        Assert.Empty(_dto.Conferencia.Divergencias);

        var indice = Array.IndexOf(_primeiras, "CONFERÊNCIA");
        Assert.Contains("conferem", _linhas[indice + 1][0]);
    }

    [Fact]
    public void CategoriasZeradasFicamDeForaPorPadrao()
    {
        var dezembro = _dto.DetalhePorMes.Single(mes => mes.Mes == "DEZ/2026");
        var rotulos = dezembro.Categorias.Select(categoria => categoria.Rotulo).ToArray();

        Assert.DoesNotContain("Faturamento", rotulos);
        Assert.Contains("Saldo Final de Caixa", rotulos);
    }

    [Fact]
    public void OpcaoIncluirZerados()
    {
        var dto = new GeradorRelatorio(
                PlanilhaExemplo.Ler(),
                OpcoesRelatorio.Padrao with { IncluirZerados = true })
            .MontarDto();

        var dezembro = dto.DetalhePorMes.Single(mes => mes.Mes == "DEZ/2026");
        Assert.Contains(dezembro.Categorias, categoria => categoria.Rotulo == "Faturamento");
    }

    [Fact]
    public void RecuoMarcaOsNiveis()
    {
        Assert.Contains(_linhas, linha => linha.Length > 0 && linha[0].StartsWith(GeradorRelatorio.Recuo));

        var semRecuo = new GeradorRelatorio(
                PlanilhaExemplo.Ler(),
                OpcoesRelatorio.Padrao with { Recuar = false })
            .MontarCsv();

        Assert.DoesNotContain(semRecuo, linha => linha.Length > 0 && linha[0].StartsWith(GeradorRelatorio.Recuo));
    }

    [Fact]
    public void DtoTrazOsMesmosNumerosDoCsv()
    {
        var janeiro = _dto.DetalhePorMes[0];
        var recebimentos = janeiro.Categorias.Single(c => c.Rotulo == "Total de Recebimentos");

        var linhaCsv = _linhas.First(linha =>
            linha.Length > 1 && linha[0] == "Total de Recebimentos" &&
            linha[1] == NumeroBr.Formatar(recebimentos.Previsto));

        Assert.Equal(NumeroBr.Formatar(recebimentos.Realizado), linhaCsv[2]);
        Assert.Equal(NumeroBr.Formatar(recebimentos.Diferenca), linhaCsv[3]);
    }

    [Fact]
    public void HierarquiaApareceNoDetalhe()
    {
        var janeiro = _dto.DetalhePorMes[0];

        Assert.Equal(0, janeiro.Categorias.Single(c => c.Rotulo == "Total de Recebimentos").Nivel);
        Assert.Equal(1, janeiro.Categorias.Single(c => c.Rotulo == "Receitas de Vendas").Nivel);
        Assert.Equal(2, janeiro.Categorias.Single(c => c.Rotulo == "Faturamento").Nivel);
    }
}

public class EscritorCsvRelatorioTests
{
    [Fact]
    public void GravaUmArquivoUnicoEmUtf8ComBom()
    {
        var pasta = Directory.CreateTempSubdirectory();
        try
        {
            var destino = Path.Combine(pasta.FullName, "consolidado.csv");
            var gerador = new GeradorRelatorio(PlanilhaExemplo.Ler());

            EscritorCsvRelatorio.Escrever(gerador.MontarCsv(), destino);

            Assert.Single(Directory.GetFiles(pasta.FullName));

            var bytes = File.ReadAllBytes(destino);
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());

            var texto = Encoding.UTF8.GetString(bytes);
            Assert.Contains("MÊS 01 - JAN/2026", texto);
            Assert.Contains("TOTAL GERAL", texto);
            Assert.Contains("\r\n", texto);
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public void ServicoDevolveOArquivoEmBytes()
    {
        using var entrada = File.OpenRead(PlanilhaExemplo.Caminho);
        var servico = new ServicoFluxoCaixa();

        var bytes = servico.Consolidar(entrada, "fluxo_de_caixa_mensal.csv");
        var texto = Encoding.UTF8.GetString(bytes);

        Assert.Contains("RESUMO POR MÊS", texto);
        Assert.Equal(
            "fluxo_de_caixa_mensal_consolidado.csv",
            ServicoFluxoCaixa.NomeSugerido("fluxo_de_caixa_mensal.csv"));
    }
}
