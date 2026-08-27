namespace FluxoCaixa.Core.Modelos;

/// <summary>
/// Uma categoria da planilha, com o previsto e o realizado de cada mes.
/// </summary>
public sealed class LinhaPlanilha
{
    private decimal[]? _valores;

    public LinhaPlanilha(
        string rotulo,
        IReadOnlyList<decimal?> previsto,
        IReadOnlyList<decimal?> realizado,
        decimal? totalPrevistoOrigem = null,
        decimal? totalRealizadoOrigem = null)
    {
        Rotulo = rotulo;
        Previsto = previsto;
        Realizado = realizado;
        TotalPrevistoOrigem = totalPrevistoOrigem;
        TotalRealizadoOrigem = totalRealizadoOrigem;
    }

    public string Rotulo { get; }

    public IReadOnlyList<decimal?> Previsto { get; }

    public IReadOnlyList<decimal?> Realizado { get; }

    /// <summary>Total do ano como veio no arquivo de origem (coluna Total).</summary>
    public decimal? TotalPrevistoOrigem { get; }

    /// <summary>Total do ano como veio no arquivo de origem (coluna Total).</summary>
    public decimal? TotalRealizadoOrigem { get; }

    /// <summary>
    /// Vetor unico com previsto e realizado de todos os meses, usado para
    /// comparar linhas na montagem da hierarquia. Celulas vazias valem zero.
    /// </summary>
    public decimal[] Valores()
    {
        if (_valores is not null)
        {
            return _valores;
        }

        var valores = new decimal[Previsto.Count + Realizado.Count];
        for (var i = 0; i < Previsto.Count; i++)
        {
            valores[i] = Previsto[i] ?? 0m;
        }

        for (var i = 0; i < Realizado.Count; i++)
        {
            valores[Previsto.Count + i] = Realizado[i] ?? 0m;
        }

        _valores = valores;
        return valores;
    }
}
