using System.Text;
using FluxoCaixa.Core.Hierarquia;
using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Tests;

public class ConstrutorHierarquiaTests
{
    private static string[] Desenhar(IReadOnlyList<NoCategoria> nos)
    {
        var linhas = new List<string>();

        void Visitar(NoCategoria no)
        {
            linhas.Add(new string(' ', no.Nivel * 2) + no.Rotulo);
            foreach (var filho in no.Filhos)
            {
                Visitar(filho);
            }
        }

        foreach (var raiz in nos)
        {
            Visitar(raiz);
        }

        return [.. linhas];
    }

    [Fact]
    public void PaiRecebeOsFilhosQueSomamOValorDele()
    {
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Grupo", 100, 90),
            PlanilhaExemplo.Linha("Item A", 60, 50),
            PlanilhaExemplo.Linha("Item B", 40, 40),
        ]);

        Assert.Equal(["Grupo", "  Item A", "  Item B"], Desenhar(arvore));
    }

    [Fact]
    public void ReconheceTresNiveis()
    {
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Grupo", 100, 100),
            PlanilhaExemplo.Linha("Subgrupo", 70, 70),
            PlanilhaExemplo.Linha("Folha 1", 30, 30),
            PlanilhaExemplo.Linha("Folha 2", 40, 40),
            PlanilhaExemplo.Linha("Outro item", 30, 30),
        ]);

        Assert.Equal(
            ["Grupo", "  Subgrupo", "    Folha 1", "    Folha 2", "  Outro item"],
            Desenhar(arvore));
    }

    [Fact]
    public void LinhasQueNaoSomamFicamNoMesmoNivel()
    {
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Item A", 100, 100),
            PlanilhaExemplo.Linha("Item B", 7, 7),
            PlanilhaExemplo.Linha("Item C", 13, 13),
        ]);

        Assert.Equal(["Item A", "Item B", "Item C"], Desenhar(arvore));
    }

    [Fact]
    public void SomaPrecisaBaterEmTodosOsMeses()
    {
        // O total do periodo bate (100 = 60 + 40), mas a distribuicao mensal nao.
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Grupo", 50, 50, 50, 50),
            PlanilhaExemplo.Linha("Item A", 60, 60, 0, 0),
            PlanilhaExemplo.Linha("Item B", 40, 40, 0, 0),
        ]);

        Assert.Equal(["Grupo", "Item A", "Item B"], Desenhar(arvore));
    }

    [Fact]
    public void LinhasDeResumoNuncaViramFilhas()
    {
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Saldo do Mês Anterior", 100, 100),
            PlanilhaExemplo.Linha("Total de Recebimentos", 60, 60),
            PlanilhaExemplo.Linha("Vendas", 60, 60),
            PlanilhaExemplo.Linha("Total de Pagamentos", -40, -40),
        ]);

        Assert.Equal(
            ["Saldo do Mês Anterior", "Total de Recebimentos", "  Vendas", "Total de Pagamentos"],
            Desenhar(arvore));
    }

    [Fact]
    public void GrupoComUmUnicoFilho()
    {
        var arvore = ConstrutorHierarquia.Montar(
        [
            PlanilhaExemplo.Linha("Despesas Financeiras", -10, -10),
            PlanilhaExemplo.Linha("Despesas Bancárias", -10, -10),
        ]);

        Assert.Equal(["Despesas Financeiras", "  Despesas Bancárias"], Desenhar(arvore));
    }
}

public class HierarquiaDaPlanilhaRealTests
{
    private readonly Planilha _planilha = PlanilhaExemplo.Ler();
    private readonly Dictionary<string, NoCategoria> _porRotulo;
    private readonly IReadOnlyList<NoCategoria> _arvore;

    public HierarquiaDaPlanilhaRealTests()
    {
        _arvore = ConstrutorHierarquia.Montar(_planilha.Linhas);
        _porRotulo = _arvore
            .SelectMany(raiz => raiz.Percorrer())
            .ToDictionary(no => no.Rotulo);
    }

    [Fact]
    public void NenhumaCategoriaSePerde()
        => Assert.Equal(_planilha.Linhas.Count, _porRotulo.Count);

    [Fact]
    public void RaizesSaoAsLinhasDeResumo()
        => Assert.Equal(
            [
                "Saldo do Mês Anterior",
                "Total de Recebimentos",
                "Total de Pagamentos",
                "Geração de Caixa do Período",
                "Total de Transferências",
                "Saldo Final de Caixa",
            ],
            _arvore.Select(no => no.Rotulo).ToArray());

    [Fact]
    public void GruposConhecidos()
    {
        Assert.Equal(
            ["Encerramento de Contrato", "Receitas de Vendas"],
            _porRotulo["Total de Recebimentos"].Filhos.Select(no => no.Rotulo).ToArray());

        Assert.Equal(
            ["Salários - Folha", "Salários - Médicos"],
            _porRotulo["Salários"].Filhos.Select(no => no.Rotulo).ToArray());

        Assert.Equal(5, _porRotulo["Total de Pagamentos"].Filhos.Count);
        Assert.Empty(_porRotulo["Faturamento"].Filhos);
    }

    [Fact]
    public void CadaGrupoEASomaDosFilhosMesAMes()
    {
        var erros = new StringBuilder();

        foreach (var no in _porRotulo.Values.Where(no => no.Filhos.Count > 0))
        {
            for (var indice = 0; indice < _planilha.Meses.Count; indice++)
            {
                var previstoPai = no.Linha.Previsto[indice] ?? 0m;
                var previstoFilhos = no.Filhos.Sum(filho => filho.Linha.Previsto[indice] ?? 0m);
                var realizadoPai = no.Linha.Realizado[indice] ?? 0m;
                var realizadoFilhos = no.Filhos.Sum(filho => filho.Linha.Realizado[indice] ?? 0m);

                if (Math.Abs(previstoPai - previstoFilhos) > ConstrutorHierarquia.Tolerancia)
                {
                    erros.AppendLine($"{no.Rotulo} / {_planilha.Meses[indice]} / previsto: {previstoPai} != {previstoFilhos}");
                }

                if (Math.Abs(realizadoPai - realizadoFilhos) > ConstrutorHierarquia.Tolerancia)
                {
                    erros.AppendLine($"{no.Rotulo} / {_planilha.Meses[indice]} / realizado: {realizadoPai} != {realizadoFilhos}");
                }
            }
        }

        Assert.Equal(string.Empty, erros.ToString());
    }
}
