using FluxoCaixa.Core.Leitura;
using FluxoCaixa.Core.Modelos;
using FluxoCaixa.Core.Relatorio;

namespace FluxoCaixa.Core;

/// <summary>
/// Porta de entrada do sistema: recebe o CSV de origem e devolve o relatorio
/// consolidado, seja como objeto (para o front) ou como arquivo unico.
/// </summary>
public sealed class ServicoFluxoCaixa
{
    /// <summary>
    /// Le a planilha e devolve o relatorio pronto para a tela.
    /// </summary>
    public RelatorioDto Analisar(Stream entrada, string nomeArquivo, OpcoesRelatorio? opcoes = null)
        => new GeradorRelatorio(Ler(entrada, nomeArquivo), opcoes).MontarDto();

    /// <summary>
    /// Le a planilha e devolve o arquivo consolidado em bytes.
    /// </summary>
    public byte[] Consolidar(Stream entrada, string nomeArquivo, OpcoesRelatorio? opcoes = null)
    {
        opcoes ??= OpcoesRelatorio.Padrao;
        var gerador = new GeradorRelatorio(Ler(entrada, nomeArquivo), opcoes);
        return EscritorCsvRelatorio.EmBytes(gerador.MontarCsv(), opcoes.Separador);
    }

    /// <summary>
    /// Nome sugerido para o arquivo gerado, a partir do nome de origem.
    /// </summary>
    public static string NomeSugerido(string nomeArquivo)
    {
        var semExtensao = Path.GetFileNameWithoutExtension(nomeArquivo);
        return string.IsNullOrWhiteSpace(semExtensao)
            ? "fluxo_de_caixa_consolidado.csv"
            : $"{semExtensao}_consolidado.csv";
    }

    private static Planilha Ler(Stream entrada, string nomeArquivo)
        => LeitorPlanilha.Ler(entrada, nomeArquivo);
}
