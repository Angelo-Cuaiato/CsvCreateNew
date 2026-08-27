namespace FluxoCaixa.Core;

/// <summary>
/// Comparacao de rotulos sem depender de acentuacao, caixa ou espacos extras.
/// </summary>
public static class Texto
{
    private const string ComAcento = "áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ";
    private const string SemAcento = "aaaaaeeeeiiiiooooouuuucAAAAAEEEEIIIIOOOOOUUUUC";

    /// <summary>
    /// Minusculas, sem acento e sem espacos duplicados.
    /// </summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var letras = new char[texto.Length];
        for (var i = 0; i < texto.Length; i++)
        {
            var posicao = ComAcento.IndexOf(texto[i]);
            letras[i] = posicao >= 0 ? SemAcento[posicao] : texto[i];
        }

        var palavras = new string(letras)
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', palavras);
    }
}
