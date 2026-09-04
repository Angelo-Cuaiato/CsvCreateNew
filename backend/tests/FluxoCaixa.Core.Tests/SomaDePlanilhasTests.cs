using FluxoCaixa.Core.Modelos;
using FluxoCaixa.Core.Relatorio;

namespace FluxoCaixa.Core.Tests;

/// <summary>
/// Somar planilhas é o que permite um total de várias empresas num arquivo só.
/// A soma acontece na origem para que hierarquia, totais e conferência
/// continuem sendo os mesmos do relatório de uma planilha.
/// </summary>
public class SomaDePlanilhasTests
{
    private static Planilha Uma(string nome, params LinhaPlanilha[] linhas)
        => new("FLUXO DE CAIXA", ["JAN/2026", "FEV/2026"], linhas, nome);

    [Fact]
    public void Uma_planilha_so_volta_ela_mesma()
    {
        var planilha = Uma("a.csv", PlanilhaExemplo.Linha("Total de Recebimentos", 10, 8, 20, 18));

        Assert.Same(planilha, SomaDePlanilhas.Somar([planilha]));
    }

    [Fact]
    public void Soma_mes_a_mes_as_categorias_de_mesmo_nome()
    {
        var soma = SomaDePlanilhas.Somar(
        [
            Uma("a.csv", PlanilhaExemplo.Linha("Total de Recebimentos", 10, 8, 20, 18)),
            Uma("b.csv", PlanilhaExemplo.Linha("Total de Recebimentos", 1, 2, 3, 4)),
        ]);

        var linha = soma.Linha("Total de Recebimentos")!;
        Assert.Equal([11m, 23m], linha.Previsto);
        Assert.Equal([10m, 22m], linha.Realizado);
    }

    [Fact]
    public void Categoria_que_existe_em_uma_so_entra_com_os_valores_dela()
    {
        var soma = SomaDePlanilhas.Somar(
        [
            Uma("a.csv", PlanilhaExemplo.Linha("Aluguel", -100, -100, -100, -100)),
            Uma("b.csv", PlanilhaExemplo.Linha("Combustível", -50, -50, -50, -50)),
        ]);

        Assert.Equal([-100m, -100m], soma.Linha("Aluguel")!.Previsto);
        Assert.Equal([-50m, -50m], soma.Linha("Combustível")!.Previsto);
    }

    [Fact]
    public void Rotulo_com_acento_ou_caixa_diferente_e_a_mesma_categoria()
    {
        var soma = SomaDePlanilhas.Somar(
        [
            Uma("a.csv", PlanilhaExemplo.Linha("Energia Elétrica", 10, 10, 10, 10)),
            Uma("b.csv", PlanilhaExemplo.Linha("ENERGIA ELETRICA", 5, 5, 5, 5)),
        ]);

        Assert.Single(soma.Linhas);
        Assert.Equal([15m, 15m], soma.Linha("Energia Elétrica")!.Previsto);
    }

    [Fact]
    public void Preserva_a_ordem_de_primeira_aparicao()
    {
        var soma = SomaDePlanilhas.Somar(
        [
            Uma("a.csv",
                PlanilhaExemplo.Linha("Total de Recebimentos", 10, 10, 10, 10),
                PlanilhaExemplo.Linha("Total de Pagamentos", -5, -5, -5, -5)),
            Uma("b.csv",
                PlanilhaExemplo.Linha("Total de Pagamentos", -1, -1, -1, -1),
                PlanilhaExemplo.Linha("Investimentos", -2, -2, -2, -2)),
        ]);

        Assert.Equal(
            ["Total de Recebimentos", "Total de Pagamentos", "Investimentos"],
            soma.Linhas.Select(linha => linha.Rotulo));
    }

    [Fact]
    public void Mes_que_so_uma_planilha_tem_entra_no_periodo()
    {
        var jan = new Planilha("F", ["JAN/2026"], [PlanilhaExemplo.Linha("Vendas", 10, 10)], "a.csv");
        var fev = new Planilha("F", ["FEV/2026"], [PlanilhaExemplo.Linha("Vendas", 20, 20)], "b.csv");

        var soma = SomaDePlanilhas.Somar([jan, fev]);

        Assert.Equal(["JAN/2026", "FEV/2026"], soma.Meses);
        Assert.Equal([10m, 20m], soma.Linha("Vendas")!.Previsto);
    }

    [Fact]
    public void Celula_sem_valor_em_todas_continua_vazia()
    {
        // Vazio e zero contam histórias diferentes: o relatório omite categoria
        // sem movimento, e transformar nulo em zero apagaria essa distinção.
        var semValor = new LinhaPlanilha("Multas", [null, null], [null, null]);

        var soma = SomaDePlanilhas.Somar([Uma("a.csv", semValor), Uma("b.csv", semValor)]);

        Assert.All(soma.Linha("Multas")!.Previsto, valor => Assert.Null(valor));
    }

    [Fact]
    public void Total_da_origem_tambem_soma_para_a_conferencia_seguir_valendo()
    {
        var a = new LinhaPlanilha("Vendas", [10m, 10m], [8m, 8m], 20m, 16m);
        var b = new LinhaPlanilha("Vendas", [5m, 5m], [4m, 4m], 10m, 8m);

        var soma = SomaDePlanilhas.Somar([Uma("a.csv", a), Uma("b.csv", b)]);

        Assert.Equal(30m, soma.Linha("Vendas")!.TotalPrevistoOrigem);
        Assert.Equal(24m, soma.Linha("Vendas")!.TotalRealizadoOrigem);
    }

    [Fact]
    public void O_relatorio_da_soma_fecha_com_a_soma_dos_relatorios()
    {
        // O teste que importa: o total de duas planilhas somadas tem que dar o
        // mesmo que somar os totais de cada uma.
        var a = PlanilhaExemplo.Ler();
        var b = PlanilhaExemplo.Ler();

        var somado = new GeradorRelatorio(SomaDePlanilhas.Somar([a, b], "a+b")).MontarDto();
        var sozinho = new GeradorRelatorio(a).MontarDto();

        foreach (var (total, individual) in somado.TotalGeral.Zip(sozinho.TotalGeral))
        {
            Assert.Equal(individual.Rotulo, total.Rotulo);
            Assert.Equal(individual.Realizado * 2, total.Realizado);
            Assert.Equal(individual.Previsto * 2, total.Previsto);
        }
    }

    [Fact]
    public void A_soma_continua_conferindo_com_a_origem()
    {
        var a = PlanilhaExemplo.Ler();
        var relatorio = new GeradorRelatorio(SomaDePlanilhas.Somar([a, a], "a+a")).MontarDto();

        Assert.True(relatorio.Conferencia.Comparavel);
        Assert.True(relatorio.Conferencia.Ok);
        Assert.Empty(relatorio.Conferencia.Divergencias);
    }
}
