using FluxoCaixa.Core;

namespace FluxoCaixa.Core.Tests;

public class NumeroBrTests
{
    [Theory]
    [InlineData("43.623.381,07", 43623381.07)]
    [InlineData("0,00", 0)]
    [InlineData("-1.234,56", -1234.56)]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("(1.234,56)", -1234.56)]
    [InlineData("1.234,56-", -1234.56)]
    public void Ler_ConverteFormatoBrasileiro(string texto, decimal esperado)
        => Assert.Equal(esperado, NumeroBr.Ler(texto));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("-")]
    [InlineData("texto")]
    public void Ler_CelulaSemNumeroViraNull(string? texto)
        => Assert.Null(NumeroBr.Ler(texto));

    [Theory]
    [InlineData(43623381.07, "43.623.381,07")]
    [InlineData(-1234.5, "-1.234,50")]
    [InlineData(0, "0,00")]
    [InlineData(999.99, "999,99")]
    public void Formatar_UsaSeparadorDeMilhar(decimal valor, string esperado)
        => Assert.Equal(esperado, NumeroBr.Formatar(valor));

    [Fact]
    public void Formatar_NullViraCelulaVazia()
        => Assert.Equal(string.Empty, NumeroBr.Formatar(null));

    [Theory]
    [InlineData("1.234.567,89")]
    [InlineData("-98,70")]
    [InlineData("0,00")]
    public void Formatar_IdaEVolta(string texto)
        => Assert.Equal(texto, NumeroBr.Formatar(NumeroBr.Ler(texto)));

    [Fact]
    public void Percentual_CalculaQuantoDoPrevistoFoiRealizado()
    {
        Assert.Equal(50m, NumeroBr.Percentual(50m, 100m));
        Assert.Equal("98,7%", NumeroBr.FormatarPercentual(98.74m));
    }

    [Fact]
    public void Percentual_PrevistoZeradoNaoTemPercentual()
    {
        Assert.Null(NumeroBr.Percentual(10m, 0m));
        Assert.Null(NumeroBr.Percentual(10m, null));
        Assert.Equal("-", NumeroBr.FormatarPercentual(null));
    }

    [Theory]
    [InlineData("Saldo do Mês Anterior", "saldo do mes anterior")]
    [InlineData("  Geração   de  Caixa ", "geracao de caixa")]
    [InlineData(null, "")]
    public void Normalizar_IgnoraAcentoCaixaEEspacos(string? texto, string esperado)
        => Assert.Equal(esperado, Texto.Normalizar(texto));
}
