namespace FluxoCaixa.Core.Relatorio;

/// <summary>
/// Ajustes de apresentacao do relatorio.
/// </summary>
public sealed record OpcoesRelatorio
{
    public static readonly OpcoesRelatorio Padrao = new();

    /// <summary>
    /// Mantem no relatorio as categorias sem movimento no mes. Por padrao elas
    /// sao omitidas para o arquivo ficar limpo.
    /// </summary>
    public bool IncluirZerados { get; init; }

    /// <summary>Recua os subitens dentro da coluna Categoria.</summary>
    public bool Recuar { get; init; } = true;

    /// <summary>Separador do arquivo gerado.</summary>
    public string Separador { get; init; } = ";";
}
