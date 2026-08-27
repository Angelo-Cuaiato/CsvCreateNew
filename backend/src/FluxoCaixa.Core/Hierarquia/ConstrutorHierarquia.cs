using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Hierarquia;

/// <summary>
/// Reconstroi a hierarquia (grupo -> subgrupo -> item) da planilha.
/// </summary>
/// <remarks>
/// O CSV de origem vem sem indentacao: todas as categorias aparecem no mesmo
/// nivel. A relacao pai/filho, porem, continua gravada nos numeros - o valor de
/// um grupo e a soma dos filhos que vem logo abaixo dele, mes a mes. Esta
/// classe refaz a arvore comparando os vetores mensais.
/// </remarks>
public sealed class ConstrutorHierarquia
{
    /// <summary>Diferenca aceita por celula (o arquivo de origem ja vem arredondado).</summary>
    public const decimal Tolerancia = 0.05m;

    /// <summary>Linhas de resumo do relatorio: nunca sao filhas de ninguem.</summary>
    private static readonly HashSet<string> Ancoras =
    [
        "saldo do mes anterior",
        "total de recebimentos",
        "total de pagamentos",
        "geracao de caixa do periodo",
        "total de transferencias",
        "saldo final de caixa",
    ];

    /// <summary>Saldos sao fotografias de um momento: nao se somam ao longo do ano.</summary>
    private static readonly HashSet<string> Saldos =
    [
        "saldo do mes anterior",
        "saldo final de caixa",
    ];

    private readonly IReadOnlyList<LinhaPlanilha> _linhas;
    private readonly decimal[][] _valores;
    private readonly decimal[] _totais;
    private readonly bool[] _ancoras;
    private readonly bool[] _saldos;

    // Sem este cache a busca pelos filhos vira exponencial em planilhas com
    // varios niveis de subtotal: o mesmo indice seria recalculado a cada passo.
    private readonly Dictionary<int, int?> _cache = [];

    private ConstrutorHierarquia(IReadOnlyList<LinhaPlanilha> linhas)
    {
        _linhas = linhas;
        _valores = linhas.Select(linha => linha.Valores()).ToArray();
        _totais = _valores.Select(valores => valores.Sum()).ToArray();
        _ancoras = linhas.Select(EhAncora).ToArray();
        _saldos = linhas.Select(EhSaldo).ToArray();
    }

    public static bool EhAncora(LinhaPlanilha linha) => Ancoras.Contains(Texto.Normalizar(linha.Rotulo));

    public static bool EhSaldo(LinhaPlanilha linha) => Saldos.Contains(Texto.Normalizar(linha.Rotulo));

    /// <summary>
    /// Monta a arvore de categorias a partir das linhas na ordem original.
    /// </summary>
    public static IReadOnlyList<NoCategoria> Montar(IReadOnlyList<LinhaPlanilha> linhas)
        => new ConstrutorHierarquia(linhas).Montar(0, linhas.Count, nivel: 0);

    private List<NoCategoria> Montar(int inicio, int limite, int nivel)
    {
        var nos = new List<NoCategoria>();
        var indice = inicio;

        while (indice < limite)
        {
            var fim = FimDosFilhos(indice);
            if (fim is null || fim > limite)
            {
                nos.Add(new NoCategoria(_linhas[indice], nivel));
                indice++;
                continue;
            }

            var no = new NoCategoria(_linhas[indice], nivel);
            no.Filhos.AddRange(Montar(indice + 1, fim.Value, nivel + 1));
            nos.Add(no);
            indice = fim.Value;
        }

        return nos;
    }

    /// <summary>
    /// Ate onde vao os filhos da linha informada, ou null se ela for uma folha.
    /// </summary>
    private int? FimDosFilhos(int indice)
    {
        if (_cache.TryGetValue(indice, out var guardado))
        {
            return guardado;
        }

        var fim = CalcularFim(indice);
        _cache[indice] = fim;
        return fim;
    }

    /// <summary>
    /// Soma as linhas seguintes - ja agrupadas em subarvores - ate bater com o
    /// valor do pai em todos os meses. Se estourar o valor do pai, se o sinal
    /// mudar ou se aparecer uma linha de resumo, conclui que nao ha filhos.
    /// </summary>
    private int? CalcularFim(int indice)
    {
        var alvo = _valores[indice];
        if (_saldos[indice] || alvo.All(valor => valor == 0m))
        {
            return null;
        }

        var totalPai = _totais[indice];
        var sinalPai = totalPai >= 0m ? 1 : -1;
        var limiteAbsoluto = Math.Abs(totalPai) + Tolerancia;

        var acumulado = new decimal[alvo.Length];
        var posicao = indice + 1;

        while (posicao < _linhas.Count)
        {
            if (_ancoras[posicao])
            {
                return null;
            }

            var totalCandidata = _totais[posicao];
            if (totalCandidata != 0m && (totalCandidata > 0m ? 1 : -1) != sinalPai)
            {
                return null;
            }

            var valores = _valores[posicao];
            for (var i = 0; i < acumulado.Length; i++)
            {
                acumulado[i] += valores[i];
            }

            if (Math.Abs(acumulado.Sum()) > limiteAbsoluto)
            {
                return null;
            }

            // A candidata pode ser um subgrupo; nesse caso ela ja engloba os
            // proprios filhos e o irmao seguinte vem depois deles.
            posicao = FimDosFilhos(posicao) ?? posicao + 1;

            if (Iguais(acumulado, alvo))
            {
                return posicao;
            }
        }

        return null;
    }

    private static bool Iguais(decimal[] a, decimal[] b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > Tolerancia)
            {
                return false;
            }
        }

        return true;
    }
}
