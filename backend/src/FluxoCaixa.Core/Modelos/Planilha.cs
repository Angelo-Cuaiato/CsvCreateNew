namespace FluxoCaixa.Core.Modelos;

/// <summary>
/// A planilha de origem inteira, ja normalizada.
/// </summary>
public sealed class Planilha
{
    public Planilha(
        string titulo,
        IReadOnlyList<string> meses,
        IReadOnlyList<LinhaPlanilha> linhas,
        string nomeArquivo = "",
        string encoding = "utf-8",
        string separador = ";",
        IReadOnlyList<string>? avisos = null)
    {
        Titulo = titulo;
        Meses = meses;
        Linhas = linhas;
        NomeArquivo = nomeArquivo;
        Encoding = encoding;
        Separador = separador;
        Avisos = avisos ?? [];
    }

    public string Titulo { get; }

    public IReadOnlyList<string> Meses { get; }

    public IReadOnlyList<LinhaPlanilha> Linhas { get; }

    public string NomeArquivo { get; }

    public string Encoding { get; }

    public string Separador { get; }

    /// <summary>Problemas encontrados na leitura que nao impedem o relatorio.</summary>
    public IReadOnlyList<string> Avisos { get; }

    /// <summary>
    /// Busca uma linha pelo rotulo, ignorando acentos, caixa e espacos extras.
    /// </summary>
    public LinhaPlanilha? Linha(string rotulo)
    {
        var alvo = Texto.Normalizar(rotulo);
        return Linhas.FirstOrDefault(linha => Texto.Normalizar(linha.Rotulo) == alvo);
    }
}
