using FluxoCaixa.Core.Relatorio;

namespace FluxoCaixa.Core.Historico;

/// <summary>
/// Uma análise guardada: o relatório inteiro e o arquivo consolidado, para que
/// voltar a ele depois não exija reenviar a planilha.
/// </summary>
/// <param name="Consolidado">
/// O CSV já pronto. Fica gravado em vez de ser gerado de novo na hora do
/// download: assim o arquivo baixado meses depois é idêntico ao que a pessoa
/// viu no dia, mesmo que o formato do relatório mude no meio.
/// </param>
public sealed record Analise(
    Guid Id,
    string Email,
    string Autor,
    string NomeArquivo,
    DateTimeOffset EnviadoEm,
    RelatorioDto Relatorio,
    byte[] Consolidado)
{
    public ResumoDeAnalise Resumo() => new(
        Id,
        Email,
        Autor,
        NomeArquivo,
        EnviadoEm,
        Relatorio.Meses.Count > 0 ? Relatorio.Meses[0] : null,
        Relatorio.Meses.Count > 0 ? Relatorio.Meses[^1] : null,
        Relatorio.Meses.Count,
        Relatorio.Conferencia.Ok,
        Relatorio.Conferencia.Comparavel);
}

/// <summary>
/// O que a lista do histórico mostra. Vem sem o relatório e sem o CSV: a lista
/// é carregada toda vez, e esses dois campos somam megabytes.
/// </summary>
public sealed record ResumoDeAnalise(
    Guid Id,
    string Email,
    string Autor,
    string NomeArquivo,
    DateTimeOffset EnviadoEm,
    string? PrimeiroMes,
    string? UltimoMes,
    int QuantidadeDeMeses,
    bool ConferenciaOk,
    bool ConferenciaComparavel);

/// <summary>Onde as análises ficam guardadas.</summary>
public interface IRepositorioAnalises
{
    /// <summary>
    /// Se o que for guardado sobrevive a um reinício. Falso quando a aplicação
    /// está sem banco - e aí a tela avisa, em vez de prometer um histórico que
    /// some no próximo deploy.
    /// </summary>
    bool Persistente { get; }

    Task GuardarAsync(Analise analise, CancellationToken cancelamento = default);

    Task<IReadOnlyList<ResumoDeAnalise>> ListarAsync(int limite = 50, CancellationToken cancelamento = default);

    Task<Analise?> PorIdAsync(Guid id, CancellationToken cancelamento = default);

    Task<bool> ApagarAsync(Guid id, CancellationToken cancelamento = default);
}
