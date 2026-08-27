using System.Collections.Frozen;

namespace FluxoCaixa.Core.Seguranca;

/// <summary>
/// Lista fixa de usuários, carregada uma vez na subida da aplicação.
/// </summary>
public sealed class RepositorioUsuariosEmMemoria : IRepositorioUsuarios
{
    private readonly FrozenDictionary<string, Usuario> _porEmail;

    public RepositorioUsuariosEmMemoria(IEnumerable<Usuario> usuarios)
    {
        _porEmail = usuarios
            .GroupBy(usuario => Normalizar(usuario.Email))
            .ToFrozenDictionary(grupo => grupo.Key, grupo => grupo.Last());
    }

    public int Quantidade => _porEmail.Count;

    public Usuario? PorEmail(string email)
        => _porEmail.GetValueOrDefault(Normalizar(email));

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
