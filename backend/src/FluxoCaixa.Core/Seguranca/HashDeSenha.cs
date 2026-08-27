using System.Security.Cryptography;

namespace FluxoCaixa.Core.Seguranca;

/// <summary>
/// Guarda e confere senhas usando PBKDF2-HMAC-SHA256.
/// </summary>
/// <remarks>
/// A senha nunca é gravada: o que fica armazenado é
/// <c>pbkdf2-sha256$iterações$salt$hash</c>, com salt aleatório por usuário. A
/// comparação é feita em tempo fixo para não vazar informação pelo tempo de
/// resposta.
/// </remarks>
public static class HashDeSenha
{
    private const string Algoritmo = "pbkdf2-sha256";

    /// <summary>Recomendação do OWASP para PBKDF2-HMAC-SHA256.</summary>
    private const int IteracoesPadrao = 210_000;

    private const int TamanhoDoSalt = 16;
    private const int TamanhoDoHash = 32;

    /// <summary>
    /// Gera o hash de uma senha nova.
    /// </summary>
    public static string Gerar(string senha, int iteracoes = IteracoesPadrao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);

        var salt = RandomNumberGenerator.GetBytes(TamanhoDoSalt);
        var hash = Derivar(senha, salt, iteracoes);

        return string.Join('$', Algoritmo, iteracoes, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Confere a senha informada contra o hash armazenado. Um hash malformado
    /// devolve <c>false</c> em vez de estourar: senha errada e cadastro
    /// quebrado dão a mesma resposta para quem está do lado de fora.
    /// </summary>
    public static bool Conferir(string? senha, string? hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado))
        {
            return false;
        }

        var partes = hashArmazenado.Split('$');
        if (partes.Length != 4 || partes[0] != Algoritmo)
        {
            return false;
        }

        if (!int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] esperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var obtido = Derivar(senha, salt, iteracoes, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(obtido, esperado);
    }

    private static byte[] Derivar(string senha, byte[] salt, int iteracoes, int tamanho = TamanhoDoHash)
        => Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, tamanho);
}
