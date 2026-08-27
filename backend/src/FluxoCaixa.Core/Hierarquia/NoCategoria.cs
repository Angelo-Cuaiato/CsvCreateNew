using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Hierarquia;

/// <summary>
/// Uma categoria e os subitens que a compoem.
/// </summary>
public sealed class NoCategoria(LinhaPlanilha linha, int nivel)
{
    public LinhaPlanilha Linha { get; } = linha;

    public int Nivel { get; } = nivel;

    public List<NoCategoria> Filhos { get; } = [];

    public string Rotulo => Linha.Rotulo;

    /// <summary>
    /// Devolve o no e todos os descendentes, em ordem de leitura.
    /// </summary>
    public IEnumerable<NoCategoria> Percorrer()
    {
        yield return this;
        foreach (var descendente in Filhos.SelectMany(filho => filho.Percorrer()))
        {
            yield return descendente;
        }
    }
}
