using System.Text;
using FluxoCaixa.Core.Leitura;
using FluxoCaixa.Core.Modelos;

namespace FluxoCaixa.Core.Tests;

/// <summary>
/// Acesso a planilha de exemplo e atalhos para montar planilhas nos testes.
/// </summary>
public static class PlanilhaExemplo
{
    public static readonly string Caminho =
        Path.Combine(AppContext.BaseDirectory, "Dados", "fluxo_de_caixa_mensal.csv");

    public static bool Existe => File.Exists(Caminho);

    public static Planilha Ler() => LeitorPlanilha.Ler(Caminho);

    /// <summary>
    /// Le um CSV escrito direto no teste.
    /// </summary>
    public static Planilha DoTexto(string conteudo, string nome = "teste.csv", Encoding? encoding = null)
    {
        var bytes = (encoding ?? Encoding.UTF8).GetBytes(conteudo);
        using var memoria = new MemoryStream(bytes);
        return LeitorPlanilha.Ler(memoria, nome);
    }

    /// <summary>
    /// Uma linha com um valor de previsto e um de realizado por mes.
    /// Os valores vem em pares: previsto1, realizado1, previsto2, realizado2...
    /// </summary>
    public static LinhaPlanilha Linha(string rotulo, params decimal[] valores)
    {
        var previsto = new List<decimal?>();
        var realizado = new List<decimal?>();

        for (var i = 0; i < valores.Length; i += 2)
        {
            previsto.Add(valores[i]);
            realizado.Add(valores[i + 1]);
        }

        return new LinhaPlanilha(rotulo, previsto, realizado);
    }

    public const string CsvExemplo =
        "FLUXO DE CAIXA;Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$);Previsto (R$);Realizado (R$)\r\n" +
        "CATEGORIAS;JAN/2026;JAN/2026;FEV/2026;FEV/2026;Total;Total\r\n" +
        "Total de Recebimentos;100,00;90,00;200,00;250,00;300,00;340,00\r\n" +
        "Vendas;100,00;90,00;200,00;250,00;300,00;340,00\r\n";
}
