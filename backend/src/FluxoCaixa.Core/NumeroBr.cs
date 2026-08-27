using System.Globalization;

namespace FluxoCaixa.Core;

/// <summary>
/// Numeros no formato brasileiro: 1.234.567,89.
/// </summary>
/// <remarks>
/// O formato e montado a mao em vez de usar CultureInfo("pt-BR") para o
/// resultado nao depender do ICU instalado no servidor.
/// </remarks>
public static class NumeroBr
{
    public static readonly NumberFormatInfo Formato = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
        NumberDecimalDigits = 2,
        NumberNegativePattern = 1,
    };

    /// <summary>
    /// Converte "1.234,56" em decimal. Devolve null quando a celula esta vazia
    /// ou nao contem um numero. Aceita o padrao contabil "(1.234,56)", o sinal
    /// no fim "1.234,56-" e o simbolo da moeda.
    /// </summary>
    public static decimal? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpo = texto.Replace("R$", string.Empty)
            .Replace('\u00a0', ' ')
            .Trim();

        var negativo = false;
        if (limpo.StartsWith('(') && limpo.EndsWith(')'))
        {
            negativo = true;
            limpo = limpo[1..^1].Trim();
        }

        if (limpo.EndsWith('-'))
        {
            negativo = true;
            limpo = limpo[..^1].Trim();
        }

        if (limpo.Length == 0 || limpo is "-" or "+")
        {
            return null;
        }

        if (!decimal.TryParse(limpo, NumberStyles.Number, Formato, out var valor))
        {
            return null;
        }

        return negativo ? -valor : valor;
    }

    /// <summary>
    /// Formata no padrao brasileiro com duas casas: -1.234.567,89.
    /// </summary>
    public static string Formatar(decimal? valor, string vazio = "")
        => valor is null ? vazio : valor.Value.ToString("N2", Formato);

    /// <summary>
    /// Formata um percentual ja calculado: 98,7%.
    /// </summary>
    public static string FormatarPercentual(decimal? valor, string vazio = "-")
        => valor is null
            ? vazio
            : Math.Round(valor.Value, 1, MidpointRounding.AwayFromZero)
                  .ToString("0.0", Formato) + "%";

    /// <summary>
    /// Quanto do previsto foi realizado. Null quando o previsto e zero ou vazio.
    /// </summary>
    public static decimal? Percentual(decimal? realizado, decimal? previsto)
    {
        if (realizado is null || previsto is null || previsto == 0m)
        {
            return null;
        }

        return Math.Round(realizado.Value / previsto.Value * 100m, 1, MidpointRounding.AwayFromZero);
    }
}
