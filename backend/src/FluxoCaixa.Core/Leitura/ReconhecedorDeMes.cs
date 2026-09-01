using System.Text.RegularExpressions;

namespace FluxoCaixa.Core.Leitura;

/// <summary>
/// Diz se o rótulo de uma coluna se parece com um mês.
/// </summary>
/// <remarks>
/// É o que separa uma planilha de fluxo de caixa de um CSV qualquer: sem esta
/// checagem, uma lista de produtos entra e sai como se fosse um relatório,
/// porque a leitura só olhava a forma do arquivo (duas linhas de cabeçalho,
/// colunas em pares) e nunca o conteúdo.
/// </remarks>
public static partial class ReconhecedorDeMes
{
    private static readonly string[] Abreviacoes =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    private static readonly string[] PorExtenso =
    [
        "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
    ];

    /// <summary>
    /// 01/2026, 1-2026, 2026-01, 2026/1.
    /// </summary>
    /// <remarks>
    /// O ponto não entra como separador de propósito: com ele, o preço "3.50"
    /// passaria por "março de 1950".
    /// </remarks>
    [GeneratedRegex(@"^(?:(0?[1-9]|1[0-2])[/\-](\d{2}|\d{4})|(\d{4})[/\-](0?[1-9]|1[0-2]))$")]
    private static partial Regex Numerico();

    /// <summary>JAN/2026, JAN-26, JANEIRO 2026, JAN (sem ano)</summary>
    [GeneratedRegex(@"^([a-z]{3,9})[/\-.\s]*(\d{2}|\d{4})?$")]
    private static partial Regex ComNome();

    public static bool Parece(string? rotulo)
    {
        var texto = Texto.Normalizar(rotulo);
        if (texto.Length == 0)
        {
            return false;
        }

        if (Numerico().IsMatch(texto))
        {
            return true;
        }

        var comNome = ComNome().Match(texto);
        if (!comNome.Success)
        {
            return false;
        }

        var nome = comNome.Groups[1].Value;
        return Abreviacoes.Contains(nome[..Math.Min(3, nome.Length)])
               && (nome.Length == 3 || PorExtenso.Contains(nome));
    }

    /// <summary>
    /// Quantos dos rótulos informados se parecem com mês.
    /// </summary>
    public static int Contar(IEnumerable<string> rotulos) => rotulos.Count(Parece);
}
