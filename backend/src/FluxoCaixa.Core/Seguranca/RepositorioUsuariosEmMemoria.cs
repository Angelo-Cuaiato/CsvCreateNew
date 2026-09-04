using System.Collections.Concurrent;

namespace FluxoCaixa.Core.Seguranca;

/// <summary>
/// Usuários na memória do processo, semeados pela configuração.
/// </summary>
/// <remarks>
/// É o que sobra quando não há banco configurado. Aceita cadastro e alteração
/// para a tela de usuários funcionar igual, mas se declara não persistente:
/// o que for criado aqui some no próximo reinício, e a tela avisa.
/// </remarks>
public sealed class RepositorioUsuariosEmMemoria : IRepositorioUsuarios
{
    private readonly ConcurrentDictionary<string, Usuario> _porEmail;

    public RepositorioUsuariosEmMemoria(IEnumerable<Usuario> usuarios)
    {
        _porEmail = new ConcurrentDictionary<string, Usuario>(
            usuarios
                .GroupBy(usuario => Normalizar(usuario.Email))
                .Select(grupo => KeyValuePair.Create(grupo.Key, grupo.Last())));
    }

    public bool Persistente => false;

    public int Quantidade => _porEmail.Count;

    public Usuario? PorEmail(string email) => _porEmail.GetValueOrDefault(Normalizar(email));

    public Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancelamento = default)
    {
        IReadOnlyList<Usuario> lista = _porEmail.Values
            .OrderBy(usuario => usuario.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(lista);
    }

    public Task<long> QuantidadeAsync(CancellationToken cancelamento = default)
        => Task.FromResult((long)_porEmail.Count);

    public Task<bool> CriarAsync(Usuario usuario, CancellationToken cancelamento = default)
        => Task.FromResult(_porEmail.TryAdd(Normalizar(usuario.Email), usuario));

    public Task<bool> TrocarSenhaAsync(string email, string senhaHash, CancellationToken cancelamento = default)
    {
        var chave = Normalizar(email);

        if (!_porEmail.TryGetValue(chave, out var atual))
        {
            return Task.FromResult(false);
        }

        _porEmail[chave] = atual with { SenhaHash = senhaHash };
        return Task.FromResult(true);
    }

    public Task<bool> ApagarAsync(string email, CancellationToken cancelamento = default)
        => Task.FromResult(_porEmail.TryRemove(Normalizar(email), out _));

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
