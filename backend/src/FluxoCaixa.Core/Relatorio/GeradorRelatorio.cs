using FluxoCaixa.Core.Hierarquia;
using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Relatorio;

/// <summary>
/// Transforma a planilha larga no relatorio consolidado: um bloco por mes e o
/// total geral no final. O mesmo conteudo sai como CSV (<see cref="MontarCsv"/>)
/// e como objeto para o front (<see cref="MontarDto"/>).
/// </summary>
public sealed class GeradorRelatorio
{
    // Espaco nao separavel (U+00A0): ao contrario do espaco comum, ele nao e
    // removido pelo Excel/LibreOffice na importacao, entao o recuo sobrevive.
    public const string Recuo = "\u00a0\u00a0\u00a0\u00a0";

    private static readonly string[] CabecalhoDetalhe =
        ["Categoria", "Previsto (R$)", "Realizado (R$)", "Diferença (R$)", "% Realizado"];

    private static readonly string[] CabecalhoResumo =
    [
        "Mês",
        "Recebimentos Previsto (R$)",
        "Recebimentos Realizado (R$)",
        "Pagamentos Previsto (R$)",
        "Pagamentos Realizado (R$)",
        "Geração de Caixa Prevista (R$)",
        "Geração de Caixa Realizada (R$)",
        "Saldo Final Realizado (R$)",
    ];

    private const string SaldoInicial = "Saldo do Mês Anterior";
    private const string Recebimentos = "Total de Recebimentos";
    private const string Pagamentos = "Total de Pagamentos";
    private const string Transferencias = "Total de Transferências";
    private const string Geracao = "Geração de Caixa do Período";
    private const string SaldoFinal = "Saldo Final de Caixa";

    private readonly Planilha _planilha;
    private readonly OpcoesRelatorio _opcoes;
    private readonly IReadOnlyList<NoCategoria> _arvore;

    public GeradorRelatorio(Planilha planilha, OpcoesRelatorio? opcoes = null)
    {
        _planilha = planilha;
        _opcoes = opcoes ?? OpcoesRelatorio.Padrao;
        _arvore = ConstrutorHierarquia.Montar(planilha.Linhas);
    }

    public IReadOnlyList<NoCategoria> Arvore => _arvore;

    private IReadOnlyList<string> Meses => _planilha.Meses;

    // ------------------------------------------------------------------ CSV

    /// <summary>
    /// Monta o arquivo consolidado inteiro, linha a linha.
    /// </summary>
    public IReadOnlyList<string[]> MontarCsv()
    {
        var linhas = new List<string[]>();
        linhas.AddRange(Cabecalho());
        linhas.AddRange(ResumoPorMesCsv());
        linhas.AddRange(DetalheDosMesesCsv());
        linhas.AddRange(TotalDoPeriodoCsv());
        linhas.AddRange(ConferenciaCsv());
        return linhas;
    }

    private IEnumerable<string[]> Cabecalho()
    {
        var periodo = Meses.Count > 0
            ? $"{Meses[0]} a {Meses[^1]} ({Meses.Count} meses)"
            : "(sem meses)";

        yield return [$"{_planilha.Titulo} - RELATÓRIO CONSOLIDADO"];
        yield return ["Arquivo de origem", string.IsNullOrEmpty(_planilha.NomeArquivo) ? "(não informado)" : _planilha.NomeArquivo];
        yield return ["Período", periodo];
        yield return ["Gerado em", DateTime.Now.ToString("dd/MM/yyyy HH:mm")];
        yield return ["Valores", "R$ - formato brasileiro (1.234,56)"];
        yield return ["Legenda", "Diferença = Realizado - Previsto | % Realizado = Realizado / Previsto"];
        yield return [];
    }

    private IEnumerable<string[]> ResumoPorMesCsv()
    {
        var resumo = MontarResumoPorMes();
        if (resumo.Count == 0)
        {
            yield break;
        }

        yield return ["RESUMO POR MÊS"];
        yield return CabecalhoResumo;

        foreach (var mes in resumo)
        {
            yield return
            [
                mes.Mes,
                NumeroBr.Formatar(mes.RecebimentosPrevisto),
                NumeroBr.Formatar(mes.RecebimentosRealizado),
                NumeroBr.Formatar(mes.PagamentosPrevisto),
                NumeroBr.Formatar(mes.PagamentosRealizado),
                NumeroBr.Formatar(mes.GeracaoPrevista),
                NumeroBr.Formatar(mes.GeracaoRealizada),
                NumeroBr.Formatar(mes.SaldoFinalRealizado),
            ];
        }

        var total = MontarTotalDoResumo();
        yield return
        [
            "TOTAL DO PERÍODO",
            NumeroBr.Formatar(total.RecebimentosPrevisto),
            NumeroBr.Formatar(total.RecebimentosRealizado),
            NumeroBr.Formatar(total.PagamentosPrevisto),
            NumeroBr.Formatar(total.PagamentosRealizado),
            NumeroBr.Formatar(total.GeracaoPrevista),
            NumeroBr.Formatar(total.GeracaoRealizada),
            NumeroBr.Formatar(total.SaldoFinalRealizado),
        ];
        yield return [];
    }

    private IEnumerable<string[]> DetalheDosMesesCsv()
    {
        for (var indice = 0; indice < Meses.Count; indice++)
        {
            yield return [$"MÊS {indice + 1:00} - {Meses[indice]}"];
            yield return CabecalhoDetalhe;

            foreach (var categoria in MontarDetalhe(indice))
            {
                yield return LinhaDetalhe(categoria);
            }

            yield return [];
        }
    }

    private IEnumerable<string[]> TotalDoPeriodoCsv()
    {
        yield return
        [
            Meses.Count > 0
                ? $"TOTAL DO PERÍODO - {Meses[0]} A {Meses[^1]}"
                : "TOTAL DO PERÍODO",
        ];
        yield return CabecalhoDetalhe;

        foreach (var categoria in MontarDetalhe(null))
        {
            yield return LinhaDetalhe(categoria);
        }

        yield return [];
        yield return ["TOTAL GERAL"];
        yield return CabecalhoDetalhe;

        foreach (var categoria in MontarTotalGeral())
        {
            yield return LinhaDetalhe(categoria);
        }

        yield return [];
    }

    private IEnumerable<string[]> ConferenciaCsv()
    {
        var conferencia = MontarConferencia();

        yield return ["CONFERÊNCIA"];

        if (!conferencia.Comparavel)
        {
            yield return
            [
                "O arquivo de origem não trazia coluna Total, então não houve o que conferir.",
            ];
        }
        else if (conferencia.Ok)
        {
            yield return ["Todos os totais somados mês a mês conferem com o total do arquivo de origem."];
        }
        else
        {
            yield return
            [
                "Categoria",
                "Coluna",
                "Total no arquivo (R$)",
                "Total somado mês a mês (R$)",
                "Diferença (R$)",
            ];

            foreach (var divergencia in conferencia.Divergencias)
            {
                yield return
                [
                    divergencia.Categoria,
                    divergencia.Coluna,
                    NumeroBr.Formatar(divergencia.TotalArquivo),
                    NumeroBr.Formatar(divergencia.TotalCalculado),
                    NumeroBr.Formatar(divergencia.Diferenca),
                ];
            }
        }

        foreach (var aviso in _planilha.Avisos)
        {
            yield return ["Aviso", aviso];
        }
    }

    private string[] LinhaDetalhe(CategoriaDto categoria)
    {
        var prefixo = _opcoes.Recuar
            ? string.Concat(Enumerable.Repeat(Recuo, categoria.Nivel))
            : string.Empty;

        return
        [
            prefixo + categoria.Rotulo,
            NumeroBr.Formatar(categoria.Previsto),
            NumeroBr.Formatar(categoria.Realizado),
            NumeroBr.Formatar(categoria.Diferenca),
            NumeroBr.FormatarPercentual(categoria.PercentualRealizado),
        ];
    }

    // ------------------------------------------------------------------ DTO

    /// <summary>
    /// O mesmo relatorio em objetos, para o front montar as telas.
    /// </summary>
    public RelatorioDto MontarDto() => new(
        _planilha.Titulo,
        _planilha.NomeArquivo,
        _planilha.Encoding,
        _planilha.Separador,
        Meses,
        MontarResumoPorMes(),
        Meses.Select((mes, indice) => new MesDto(mes, MontarDetalhe(indice))).ToArray(),
        MontarDetalhe(null),
        MontarTotalGeral(),
        MontarConferencia(),
        _planilha.Avisos);

    private IReadOnlyList<ResumoMesDto> MontarResumoPorMes()
    {
        var recebimentos = _planilha.Linha(Recebimentos);
        var pagamentos = _planilha.Linha(Pagamentos);
        var geracao = _planilha.Linha(Geracao);
        var saldoFinal = _planilha.Linha(SaldoFinal);

        if (recebimentos is null && pagamentos is null && geracao is null)
        {
            return [];
        }

        return Meses.Select((mes, indice) => new ResumoMesDto(
            mes,
            recebimentos?.Previsto[indice],
            recebimentos?.Realizado[indice],
            pagamentos?.Previsto[indice],
            pagamentos?.Realizado[indice],
            geracao?.Previsto[indice],
            geracao?.Realizado[indice],
            saldoFinal?.Realizado[indice])).ToArray();
    }

    private ResumoMesDto MontarTotalDoResumo()
    {
        var recebimentos = ValoresDoPeriodo(_planilha.Linha(Recebimentos));
        var pagamentos = ValoresDoPeriodo(_planilha.Linha(Pagamentos));
        var geracao = ValoresDoPeriodo(_planilha.Linha(Geracao));
        var saldoFinal = ValoresDoPeriodo(_planilha.Linha(SaldoFinal));

        return new ResumoMesDto(
            "TOTAL DO PERÍODO",
            recebimentos.Previsto,
            recebimentos.Realizado,
            pagamentos.Previsto,
            pagamentos.Realizado,
            geracao.Previsto,
            geracao.Realizado,
            saldoFinal.Realizado);
    }

    /// <summary>
    /// A arvore de categorias de um mes, ou do periodo inteiro quando o indice
    /// e null.
    /// </summary>
    private IReadOnlyList<CategoriaDto> MontarDetalhe(int? indice)
    {
        var categorias = new List<CategoriaDto>();

        void Visitar(NoCategoria no)
        {
            Par par;
            bool relevante;

            if (indice is null)
            {
                par = ValoresDoPeriodo(no.Linha);
                relevante = !par.Zerado;
            }
            else
            {
                par = ValoresDoMes(no.Linha, indice.Value);
                relevante = !SubtotalDoMes(no, indice.Value).Zerado;
            }

            if (!relevante && !_opcoes.IncluirZerados && !ConstrutorHierarquia.EhSaldo(no.Linha))
            {
                return;
            }

            categorias.Add(new CategoriaDto(
                no.Rotulo,
                no.Nivel,
                par.Previsto,
                par.Realizado,
                par.Diferenca,
                NumeroBr.Percentual(par.Realizado, par.Previsto)));

            foreach (var filho in no.Filhos)
            {
                Visitar(filho);
            }
        }

        foreach (var raiz in _arvore)
        {
            Visitar(raiz);
        }

        return categorias;
    }

    /// <summary>
    /// O total de tudo, em poucas linhas, para fechar o relatorio.
    /// </summary>
    private IReadOnlyList<CategoriaDto> MontarTotalGeral()
    {
        (string Rotulo, string Origem)[] itens =
        [
            ("Saldo inicial do período", SaldoInicial),
            ("Total de recebimentos", Recebimentos),
            ("Total de pagamentos", Pagamentos),
            ("Total de transferências", Transferencias),
            ("Geração de caixa do período", Geracao),
            ("Saldo final do período", SaldoFinal),
        ];

        return itens
            .Select(item => (item.Rotulo, Linha: _planilha.Linha(item.Origem)))
            .Where(item => item.Linha is not null)
            .Select(item =>
            {
                var par = ValoresDoPeriodo(item.Linha);
                return new CategoriaDto(
                    item.Rotulo,
                    0,
                    par.Previsto,
                    par.Realizado,
                    par.Diferenca,
                    NumeroBr.Percentual(par.Realizado, par.Previsto));
            })
            .ToArray();
    }

    /// <summary>
    /// Compara o total calculado com o total que veio no arquivo de origem.
    /// </summary>
    private ConferenciaDto MontarConferencia()
    {
        var divergencias = new List<DivergenciaDto>();
        var comparadas = 0;

        foreach (var linha in _planilha.Linhas)
        {
            if (ConstrutorHierarquia.EhSaldo(linha))
            {
                continue;
            }

            var calculado = ValoresDoPeriodo(linha);

            (string Coluna, decimal? Arquivo, decimal? Calculado)[] colunas =
            [
                ("Previsto", linha.TotalPrevistoOrigem, calculado.Previsto),
                ("Realizado", linha.TotalRealizadoOrigem, calculado.Realizado),
            ];

            foreach (var (coluna, arquivo, obtido) in colunas)
            {
                if (arquivo is null || obtido is null)
                {
                    continue;
                }

                comparadas++;

                if (Math.Abs(arquivo.Value - obtido.Value) <= ConstrutorHierarquia.Tolerancia)
                {
                    continue;
                }

                divergencias.Add(new DivergenciaDto(
                    linha.Rotulo,
                    coluna,
                    arquivo.Value,
                    obtido.Value,
                    obtido.Value - arquivo.Value));
            }
        }

        return new ConferenciaDto(divergencias.Count == 0 && comparadas > 0, comparadas > 0, divergencias);
    }

    // --------------------------------------------------------------- apoio

    private readonly record struct Par(decimal? Previsto, decimal? Realizado)
    {
        public decimal? Diferenca => Previsto is null && Realizado is null
            ? null
            : (Realizado ?? 0m) - (Previsto ?? 0m);

        public bool Zerado => (Previsto ?? 0m) == 0m && (Realizado ?? 0m) == 0m;
    }

    private static Par ValoresDoMes(LinhaPlanilha linha, int indice)
        => new(linha.Previsto[indice], linha.Realizado[indice]);

    /// <summary>
    /// Total do ano. Saldos nao se somam: valem o inicio e o fim do periodo.
    /// </summary>
    private Par ValoresDoPeriodo(LinhaPlanilha? linha)
    {
        if (linha is null)
        {
            return new Par(null, null);
        }

        if (ConstrutorHierarquia.EhSaldo(linha))
        {
            return Texto.Normalizar(linha.Rotulo) == Texto.Normalizar(SaldoInicial)
                ? ValoresDoMes(linha, 0)
                : ValoresDoMes(linha, Meses.Count - 1);
        }

        return new Par(
            linha.Previsto.Sum(valor => valor ?? 0m),
            linha.Realizado.Sum(valor => valor ?? 0m));
    }

    /// <summary>
    /// Soma o no e os descendentes - so para saber se o mes esta zerado.
    /// </summary>
    private static Par SubtotalDoMes(NoCategoria no, int indice)
    {
        var previsto = 0m;
        var realizado = 0m;

        foreach (var atual in no.Percorrer())
        {
            previsto += atual.Linha.Previsto[indice] ?? 0m;
            realizado += atual.Linha.Realizado[indice] ?? 0m;
        }

        return new Par(previsto, realizado);
    }
}
