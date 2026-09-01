using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Api.Seguranca;

/// <summary>
/// Configuração do token, lida da seção "Jwt" do appsettings / variáveis de
/// ambiente.
/// </summary>
public sealed class OpcoesJwt
{
    public const string Secao = "Jwt";

    /// <summary>Tamanho mínimo da chave: 256 bits, o mesmo do HMAC-SHA256.</summary>
    public const int TamanhoMinimoDaChave = 32;

    [Required]
    public string Emissor { get; set; } = "fluxo-caixa-api";

    [Required]
    public string Audiencia { get; set; } = "fluxo-caixa-web";

    /// <summary>
    /// Segredo que assina o token. Em produção venha de variável de ambiente
    /// (<c>Jwt__ChaveSecreta</c>) ou de um cofre, nunca do arquivo versionado.
    /// </summary>
    [Required]
    public string ChaveSecreta { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public int MinutosDeValidade { get; set; } = 60;

    public SymmetricSecurityKey Chave() => new(Encoding.UTF8.GetBytes(ChaveSecreta));

    /// <summary>
    /// Trechos que só aparecem nas chaves de exemplo do repositório. Nenhuma
    /// delas pode assinar token de verdade.
    /// </summary>
    private static readonly string[] MarcasDeExemplo =
    [
        "troque",
        "apenas-de-desenvolvimento",
        "somente-de-desenvolvimento",
        "chave-de-teste",
        "exemplo",
    ];

    /// <summary>
    /// Reclama cedo, na subida, em vez de deixar a aplicação assinar tokens com
    /// uma chave fraca.
    /// </summary>
    /// <param name="producao">
    /// Em produção a checagem é mais dura: chave de exemplo derruba a subida.
    /// Fora dela vale só o tamanho, para não atrapalhar quem está desenvolvendo.
    /// </param>
    public void Validar(bool producao = false)
    {
        if (string.IsNullOrWhiteSpace(ChaveSecreta))
        {
            throw new InvalidOperationException(
                "Jwt:ChaveSecreta não foi configurada. Defina a variável de ambiente Jwt__ChaveSecreta.");
        }

        if (Encoding.UTF8.GetByteCount(ChaveSecreta) < TamanhoMinimoDaChave)
        {
            throw new InvalidOperationException(
                $"Jwt:ChaveSecreta precisa ter pelo menos {TamanhoMinimoDaChave} bytes (256 bits).");
        }

        if (producao && EhDeExemplo(ChaveSecreta))
        {
            throw new InvalidOperationException(
                "Jwt:ChaveSecreta ainda é a chave de exemplo do repositório. Gere uma própria " +
                "com \"openssl rand -base64 48\" e passe em Jwt__ChaveSecreta.");
        }
    }

    /// <summary>Se o valor veio do .env.example ou do appsettings de desenvolvimento.</summary>
    public static bool EhDeExemplo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.ToLowerInvariant();
        return MarcasDeExemplo.Any(marca => texto.Contains(marca, StringComparison.Ordinal));
    }
}
