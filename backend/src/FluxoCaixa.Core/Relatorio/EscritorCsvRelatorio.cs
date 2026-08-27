using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace FluxoCaixa.Core.Relatorio;

/// <summary>
/// Grava o relatorio consolidado como um unico arquivo CSV.
/// </summary>
public static class EscritorCsvRelatorio
{
    /// <summary>
    /// UTF-8 com BOM: e assim que o Excel abre os acentos corretamente.
    /// </summary>
    private static readonly Encoding Saida = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static void Escrever(IReadOnlyList<string[]> linhas, Stream destino, string separador = ";")
    {
        using var texto = new StreamWriter(destino, Saida, leaveOpen: true);
        Escrever(linhas, texto, separador);
    }

    public static void Escrever(IReadOnlyList<string[]> linhas, string caminho, string separador = ";")
    {
        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminho));
        if (!string.IsNullOrEmpty(pasta))
        {
            Directory.CreateDirectory(pasta);
        }

        using var texto = new StreamWriter(caminho, append: false, Saida);
        Escrever(linhas, texto, separador);
    }

    /// <summary>
    /// Devolve o arquivo inteiro em bytes, pronto para download.
    /// </summary>
    public static byte[] EmBytes(IReadOnlyList<string[]> linhas, string separador = ";")
    {
        using var memoria = new MemoryStream();
        Escrever(linhas, memoria, separador);
        return memoria.ToArray();
    }

    private static void Escrever(IReadOnlyList<string[]> linhas, TextWriter destino, string separador)
    {
        var configuracao = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = separador,
            HasHeaderRecord = false,
            NewLine = "\r\n",
        };

        using var csv = new CsvWriter(destino, configuracao);

        foreach (var linha in linhas)
        {
            foreach (var campo in linha)
            {
                csv.WriteField(campo);
            }

            csv.NextRecord();
        }
    }
}
