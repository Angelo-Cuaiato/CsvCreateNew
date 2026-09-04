using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core;

/// <summary>
/// Soma várias planilhas numa só, para o relatório consolidado de todas.
/// </summary>
/// <remarks>
/// A soma acontece aqui, no modelo de origem, e não no relatório pronto. Assim
/// tudo o que vem depois - hierarquia, resumo por mês, totais, conferência e o
/// arquivo CSV - é o mesmo código que já roda para uma planilha só, e o
/// resultado sai com a mesma cara.
/// </remarks>
public static class SomaDePlanilhas
{
    /// <summary>
    /// Uma planilha com os meses de todas e cada categoria somada.
    /// </summary>
    /// <param name="planilhas">Pelo menos uma. Com uma só, devolve ela mesma.</param>
    /// <param name="nome">Nome que aparece como arquivo de origem no relatório.</param>
    public static Planilha Somar(IReadOnlyList<Planilha> planilhas, string nome = "")
    {
        ArgumentOutOfRangeException.ThrowIfZero(planilhas.Count);

        if (planilhas.Count == 1)
        {
            return planilhas[0];
        }

        var meses = MesesNaOrdem(planilhas);

        // A lista mantém a ordem de primeira aparição - recebimentos antes de
        // pagamentos, como nas planilhas de origem. O dicionário só acha.
        var acumuladores = new List<Acumulador>();
        var porRotulo = new Dictionary<string, Acumulador>();

        foreach (var planilha in planilhas)
        {
            // Onde cada mês desta planilha cai na lista combinada.
            var destino = planilha.Meses.Select(mes => meses.IndexOf(mes)).ToArray();

            foreach (var linha in planilha.Linhas)
            {
                var chave = Texto.Normalizar(linha.Rotulo);

                if (!porRotulo.TryGetValue(chave, out var acumulador))
                {
                    acumulador = new Acumulador(linha.Rotulo, meses.Count);
                    porRotulo[chave] = acumulador;
                    acumuladores.Add(acumulador);
                }

                acumulador.Somar(linha, destino);
            }
        }

        return new Planilha(
            planilhas[0].Titulo,
            meses,
            [.. acumuladores.Select(acumulador => acumulador.Fechar())],
            nome,
            planilhas[0].Encoding,
            planilhas[0].Separador,
            [.. planilhas.SelectMany(planilha => planilha.Avisos).Distinct()]);
    }

    /// <summary>
    /// Os meses de todas as planilhas, sem repetir e preservando a ordem em que
    /// aparecem. Planilhas do mesmo período saem com o período delas.
    /// </summary>
    private static List<string> MesesNaOrdem(IReadOnlyList<Planilha> planilhas)
    {
        var meses = new List<string>();

        foreach (var mes in planilhas.SelectMany(planilha => planilha.Meses))
        {
            if (!meses.Contains(mes))
            {
                meses.Add(mes);
            }
        }

        return meses;
    }

    /// <summary>
    /// Acumula uma categoria ao longo das planilhas. O valor fica nulo enquanto
    /// nenhuma planilha trouxer número para aquele mês: célula vazia e zero
    /// contam histórias diferentes, e o relatório omite categorias sem
    /// movimento.
    /// </summary>
    private sealed class Acumulador(string rotulo, int meses)
    {
        private readonly decimal?[] _previsto = new decimal?[meses];
        private readonly decimal?[] _realizado = new decimal?[meses];
        private decimal? _totalPrevisto;
        private decimal? _totalRealizado;

        public void Somar(LinhaPlanilha linha, int[] destino)
        {
            for (var i = 0; i < destino.Length; i++)
            {
                var coluna = destino[i];
                if (coluna < 0)
                {
                    continue;
                }

                _previsto[coluna] = Mais(_previsto[coluna], Em(linha.Previsto, i));
                _realizado[coluna] = Mais(_realizado[coluna], Em(linha.Realizado, i));
            }

            // A coluna Total da origem também soma: é contra ela que a
            // conferência do relatório compara.
            _totalPrevisto = Mais(_totalPrevisto, linha.TotalPrevistoOrigem);
            _totalRealizado = Mais(_totalRealizado, linha.TotalRealizadoOrigem);
        }

        public LinhaPlanilha Fechar() =>
            new(rotulo, _previsto, _realizado, _totalPrevisto, _totalRealizado);

        private static decimal? Em(IReadOnlyList<decimal?> valores, int i)
            => i < valores.Count ? valores[i] : null;

        private static decimal? Mais(decimal? acumulado, decimal? parcela) => (acumulado, parcela) switch
        {
            (null, null) => null,
            (null, var valor) => valor,
            (var valor, null) => valor,
            var (a, b) => a + b,
        };
    }
}
