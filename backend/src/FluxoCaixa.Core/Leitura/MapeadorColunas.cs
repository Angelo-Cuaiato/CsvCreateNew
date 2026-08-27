namespace FluxoCaixa.Core.Leitura;

/// <summary>
/// Descobre, a partir das duas linhas de cabecalho, quais colunas sao o
/// previsto e o realizado de cada mes e qual e o par com o total do ano.
/// </summary>
public static class MapeadorColunas
{
    public readonly record struct ColunasDoMes(string Mes, int Previsto, int Realizado);

    public readonly record struct ColunasDoTotal(int Previsto, int Realizado);

    public sealed record Mapa(IReadOnlyList<ColunasDoMes> Meses, ColunasDoTotal? Total);

    public static Mapa Mapear(string[] cabecalhoMeses, string[] cabecalhoTipos)
    {
        var meses = new List<ColunasDoMes>();
        ColunasDoTotal? total = null;

        var indice = 1;
        while (indice < cabecalhoMeses.Length)
        {
            var rotulo = cabecalhoMeses[indice].Trim();
            if (rotulo.Length == 0)
            {
                indice++;
                continue;
            }

            var seguinte = indice + 1 < cabecalhoMeses.Length
                ? cabecalhoMeses[indice + 1].Trim()
                : string.Empty;

            // O mes aparece duas vezes seguidas: uma para previsto, outra para
            // realizado. Quando aparece sozinho, a mesma coluna serve para os dois.
            var emPar = Texto.Normalizar(seguinte) == Texto.Normalizar(rotulo)
                        && Texto.Normalizar(rotulo).Length > 0;

            var previsto = indice;
            var realizado = emPar ? indice + 1 : indice;

            // A linha de cima diz qual das duas colunas e o realizado.
            if (emPar && EhRealizado(cabecalhoTipos, previsto))
            {
                (previsto, realizado) = (realizado, previsto);
            }

            if (Texto.Normalizar(rotulo).StartsWith("total", StringComparison.Ordinal))
            {
                total = new ColunasDoTotal(previsto, realizado);
            }
            else
            {
                meses.Add(new ColunasDoMes(rotulo, previsto, realizado));
            }

            indice += emPar ? 2 : 1;
        }

        return new Mapa(meses, total);
    }

    private static bool EhRealizado(string[] cabecalhoTipos, int indice)
        => indice < cabecalhoTipos.Length
           && Texto.Normalizar(cabecalhoTipos[indice]).Contains("realizado", StringComparison.Ordinal);
}
