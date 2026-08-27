namespace FluxoCaixa.Core.Seguranca;

/// <summary>
/// Quem pode entrar no sistema. A senha só existe aqui como hash.
/// </summary>
public sealed record Usuario(string Email, string Nome, string SenhaHash, string Perfil = Perfis.Usuario);

/// <summary>Perfis reconhecidos pela API.</summary>
public static class Perfis
{
    public const string Usuario = "usuario";
    public const string Administrador = "administrador";
}

/// <summary>
/// De onde vêm os usuários. Hoje a implementação é em memória, alimentada pela
/// configuração; trocar por banco é implementar esta interface.
/// </summary>
public interface IRepositorioUsuarios
{
    /// <summary>Busca pelo e-mail, ignorando caixa. Null quando não existe.</summary>
    Usuario? PorEmail(string email);
}
