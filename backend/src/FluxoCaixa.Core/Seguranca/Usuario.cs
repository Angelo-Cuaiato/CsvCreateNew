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
/// De onde vêm os usuários: do PostgreSQL quando há banco, da configuração
/// quando não há.
/// </summary>
public interface IRepositorioUsuarios
{
    /// <summary>
    /// Se o que for cadastrado aqui sobrevive a um reinício. Falso sem banco -
    /// e aí a tela avisa, em vez de prometer um cadastro que some no próximo
    /// deploy.
    /// </summary>
    bool Persistente { get; }

    /// <summary>Busca pelo e-mail, ignorando caixa. Null quando não existe.</summary>
    Usuario? PorEmail(string email);

    /// <summary>Todos os usuários, em ordem de e-mail.</summary>
    Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancelamento = default);

    /// <summary>Quantos existem — usado para decidir a carga inicial.</summary>
    Task<long> QuantidadeAsync(CancellationToken cancelamento = default);

    /// <summary>
    /// Cadastra. Devolve false quando o e-mail já existe, em vez de estourar.
    /// </summary>
    Task<bool> CriarAsync(Usuario usuario, CancellationToken cancelamento = default);

    /// <summary>Troca o hash da senha. False quando o usuário não existe.</summary>
    Task<bool> TrocarSenhaAsync(string email, string senhaHash, CancellationToken cancelamento = default);

    /// <summary>Apaga. False quando o usuário não existe.</summary>
    Task<bool> ApagarAsync(string email, CancellationToken cancelamento = default);
}
