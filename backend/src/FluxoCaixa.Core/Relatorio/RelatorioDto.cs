namespace FluxoCaixa.Core.Relatorio;

/// <summary>Uma categoria do relatorio, com o nivel dela na hierarquia.</summary>
public sealed record CategoriaDto(
    string Rotulo,
    int Nivel,
    decimal? Previsto,
    decimal? Realizado,
    decimal? Diferenca,
    decimal? PercentualRealizado);

/// <summary>O detalhamento de um mes.</summary>
public sealed record MesDto(string Mes, IReadOnlyList<CategoriaDto> Categorias);

/// <summary>Uma linha do resumo: o mes em numeros redondos.</summary>
public sealed record ResumoMesDto(
    string Mes,
    decimal? RecebimentosPrevisto,
    decimal? RecebimentosRealizado,
    decimal? PagamentosPrevisto,
    decimal? PagamentosRealizado,
    decimal? GeracaoPrevista,
    decimal? GeracaoRealizada,
    decimal? SaldoFinalRealizado);

/// <summary>Um total que nao fecha com o que veio no arquivo de origem.</summary>
public sealed record DivergenciaDto(
    string Categoria,
    string Coluna,
    decimal TotalArquivo,
    decimal TotalCalculado,
    decimal Diferenca);

/// <summary>
/// Resultado da conferência contra a coluna Total da origem.
/// </summary>
/// <param name="Ok">Os totais batem. Só faz sentido quando <paramref name="Comparavel"/> é true.</param>
/// <param name="Comparavel">
/// O arquivo de origem trazia coluna Total. Quando é false não houve o que
/// comparar — e dizer "confere" seria enganoso.
/// </param>
public sealed record ConferenciaDto(bool Ok, bool Comparavel, IReadOnlyList<DivergenciaDto> Divergencias);

/// <summary>O relatorio inteiro, no formato que o front consome.</summary>
public sealed record RelatorioDto(
    string Titulo,
    string Arquivo,
    string Encoding,
    string Separador,
    IReadOnlyList<string> Meses,
    IReadOnlyList<ResumoMesDto> ResumoPorMes,
    IReadOnlyList<MesDto> DetalhePorMes,
    IReadOnlyList<CategoriaDto> TotalDoPeriodo,
    IReadOnlyList<CategoriaDto> TotalGeral,
    ConferenciaDto Conferencia,
    IReadOnlyList<string> Avisos);
