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
    /// Reclama cedo, na subida, em vez de deixar a aplicação assinar tokens com
    /// uma chave fraca.
    /// </summary>
    public void Validar()
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
    }
}
