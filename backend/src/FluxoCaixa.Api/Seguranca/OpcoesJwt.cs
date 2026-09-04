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
                "Jwt:ChaveSecreta não foi configurada. Defina a variável de ambiente "
                + "Jwt__ChaveSecreta (dois sublinhados, não dois pontos). "
                + PistaSobreOAmbiente(Environment.GetEnvironmentVariables().Keys.Cast<object>()
                    .Select(chave => chave.ToString() ?? string.Empty)));
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

    /// <summary>
    /// Nome que o docker compose usa no .env e traduz para Jwt__ChaveSecreta. Fora
    /// do compose ninguém faz essa tradução, e é o engano mais comum ao subir em
    /// uma plataforma de deploy.
    /// </summary>
    private const string NomeDoCompose = "JWT_CHAVE";

    /// <summary>
    /// Transforma "não foi configurada" em algo que se resolve lendo o log: diz o
    /// que chegou de fato neste contêiner. Só nomes de variáveis, nunca valores -
    /// esta mensagem vai para o log da plataforma.
    /// </summary>
    public static string PistaSobreOAmbiente(IEnumerable<string> nomesDoAmbiente)
    {
        var nomes = nomesDoAmbiente.ToList();

        if (nomes.Contains(NomeDoCompose, StringComparer.OrdinalIgnoreCase))
        {
            return $"Encontrei {NomeDoCompose}: esse nome existe só dentro do docker compose, "
                + "que o traduz para Jwt__ChaveSecreta. Fora do compose, use o nome final.";
        }

        var parecidas = nomes
            .Where(nome => nome.Contains("JWT", StringComparison.OrdinalIgnoreCase))
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        if (parecidas.Count > 0)
        {
            return $"Variáveis com \"Jwt\" no nome que chegaram aqui: {string.Join(", ", parecidas)}. "
                + "Nenhuma delas é Jwt__ChaveSecreta.";
        }

        return $"Nenhuma variável com \"Jwt\" no nome chegou neste contêiner "
            + $"(das {nomes.Count} presentes). Confira se elas foram salvas neste serviço "
            + "e se o deploy foi aplicado depois de salvar.";
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
