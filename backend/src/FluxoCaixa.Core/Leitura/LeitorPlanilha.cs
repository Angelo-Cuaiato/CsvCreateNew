using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Leitura;

/// <summary>
/// Le a planilha de fluxo de caixa exportada em CSV (formato largo).
/// </summary>
/// <remarks>
/// O arquivo de origem tem duas linhas de cabecalho:
/// <code>
/// FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);...
/// CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026;...;Total;Total
/// </code>
/// e uma linha por categoria, sem indentacao, com duas colunas por mes
/// (previsto e realizado) e um par final com o total do ano.
/// </remarks>
public static class LeitorPlanilha
{
    private static readonly string[] Separadores = [";", ",", "\t"];

    static LeitorPlanilha()
    {
        // Necessario para abrir arquivos em windows-1252 fora do Windows.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Planilha Ler(string caminho, string? separador = null)
    {
        using var arquivo = File.OpenRead(caminho);
        return Ler(arquivo, Path.GetFileName(caminho), separador);
    }

    public static Planilha Ler(Stream entrada, string nomeArquivo, string? separador = null)
    {
        using var memoria = new MemoryStream();
        entrada.CopyTo(memoria);

        var (texto, encoding) = Decodificar(memoria.ToArray());
        separador ??= DetectarSeparador(texto);

        var tabela = LerTabela(texto, separador);
        if (tabela.Count < 3)
        {
            throw new PlanilhaInvalidaException(
                "O arquivo precisa ter duas linhas de cabeçalho e pelo menos uma de dados.");
        }

        var cabecalhoTipos = tabela[0];
        var cabecalhoMeses = tabela[1];
        var titulo = cabecalhoTipos.Length > 0 && !string.IsNullOrWhiteSpace(cabecalhoTipos[0])
            ? cabecalhoTipos[0].Trim()
            : "FLUXO DE CAIXA";

        var mapa = MapeadorColunas.Mapear(cabecalhoMeses, cabecalhoTipos);
        if (mapa.Meses.Count == 0)
        {
            throw new PlanilhaInvalidaException(
                "Nenhuma coluna de mês foi encontrada na segunda linha do cabeçalho.");
        }

        var avisos = new List<string>();
        var linhas = new List<LinhaPlanilha>();

        foreach (var bruta in tabela.Skip(2))
        {
            var rotulo = bruta.Length > 0 ? bruta[0].Trim() : string.Empty;
            if (rotulo.Length == 0)
            {
                continue;
            }

            var previsto = mapa.Meses.Select(par => Celula(bruta, par.Previsto)).ToArray();
            var realizado = mapa.Meses.Select(par => Celula(bruta, par.Realizado)).ToArray();

            if (bruta.Length != cabecalhoMeses.Length)
            {
                avisos.Add(
                    $"A linha \"{rotulo}\" tem {bruta.Length} colunas e o cabeçalho tem " +
                    $"{cabecalhoMeses.Length}; as colunas faltantes foram tratadas como vazias.");
            }

            linhas.Add(new LinhaPlanilha(
                rotulo,
                previsto,
                realizado,
                mapa.Total is null ? null : Celula(bruta, mapa.Total.Value.Previsto),
                mapa.Total is null ? null : Celula(bruta, mapa.Total.Value.Realizado)));
        }

        return new Planilha(titulo, mapa.Meses.Select(m => m.Mes).ToArray(), linhas, nomeArquivo, encoding, separador, avisos);
    }

    private static decimal? Celula(string[] linha, int indice)
        => indice >= 0 && indice < linha.Length ? NumeroBr.Ler(linha[indice]) : null;

    private static List<string[]> LerTabela(string texto, string separador)
    {
        var configuracao = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = separador,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.None,
        };

        using var leitor = new StringReader(texto);
        using var csv = new CsvParser(leitor, configuracao);

        var tabela = new List<string[]>();
        while (csv.Read())
        {
            var registro = csv.Record;
            if (registro is not null && registro.Any(celula => !string.IsNullOrWhiteSpace(celula)))
            {
                tabela.Add(registro);
            }
        }

        return tabela;
    }

    /// <summary>
    /// Exportacoes brasileiras costumam vir em windows-1252; tenta UTF-8 antes.
    /// </summary>
    private static (string Texto, string Encoding) Decodificar(byte[] bruto)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return (utf8.GetString(bruto).TrimStart('﻿'), "utf-8");
        }
        catch (DecoderFallbackException)
        {
            var windows1252 = Encoding.GetEncoding(1252);
            return (windows1252.GetString(bruto), "windows-1252");
        }
    }

    private static string DetectarSeparador(string texto)
    {
        var primeira = texto.Split('\n').FirstOrDefault() ?? string.Empty;
        return Separadores
            .OrderByDescending(separador => primeira.Split(separador).Length)
            .First();
    }
}

/// <summary>
/// O arquivo enviado nao tem o formato esperado de fluxo de caixa.
/// </summary>
public sealed class PlanilhaInvalidaException(string mensagem) : Exception(mensagem);
