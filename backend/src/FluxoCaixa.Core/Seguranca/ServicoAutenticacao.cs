namespace FluxoCaixa.Core.Seguranca;

/// <summary>
/// Confere as credenciais do login.
/// </summary>
public sealed class ServicoAutenticacao(IRepositorioUsuarios repositorio)
{
    /// <summary>
    /// Hash descartável usado quando o e-mail não existe. Serve para gastar o
    /// mesmo tempo de um login válido e não denunciar quais e-mails existem.
    /// </summary>
    private static readonly string HashFalso = HashDeSenha.Gerar(Guid.NewGuid().ToString());

    /// <summary>
    /// Devolve o usuário quando e-mail e senha conferem; null caso contrário.
    /// </summary>
    public Usuario? Autenticar(string? email, string? senha)
    {
        var usuario = string.IsNullOrWhiteSpace(email) ? null : repositorio.PorEmail(email);

        if (usuario is null)
        {
            HashDeSenha.Conferir(senha, HashFalso);
            return null;
        }

        return HashDeSenha.Conferir(senha, usuario.SenhaHash) ? usuario : null;
    }
}
