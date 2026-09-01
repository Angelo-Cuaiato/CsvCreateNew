using FluxoCaixa.Core.Seguranca;
using Npgsql;

namespace FluxoCaixa.Dados;

/// <summary>
/// Usuários guardados no PostgreSQL.
/// </summary>
public sealed class RepositorioUsuariosPostgres(NpgsqlDataSource fonte) : IRepositorioUsuarios
{
    private const string BuscaPorEmail = """
        SELECT email, nome, senha_hash, perfil
          FROM usuarios
         WHERE lower(email) = @email
        """;

    private const string Insercao = """
        INSERT INTO usuarios (email, nome, senha_hash, perfil)
        VALUES (@email, @nome, @senha_hash, @perfil)
        ON CONFLICT (email) DO NOTHING
        """;

    public Usuario? PorEmail(string email)
    {
        // A interface é síncrona porque o resto do sistema não precisa de mais
        // que isso; a consulta é por chave primária.
        using var comando = fonte.CreateCommand(BuscaPorEmail);
        comando.Parameters.AddWithValue("email", Normalizar(email));

        using var leitor = comando.ExecuteReader();
        if (!leitor.Read())
        {
            return null;
        }

        return new Usuario(
            leitor.GetString(0),
            leitor.GetString(1),
            leitor.GetString(2),
            leitor.GetString(3));
    }

    /// <summary>Quantos usuários existem — usado para decidir a carga inicial.</summary>
    public async Task<long> QuantidadeAsync(CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand("SELECT count(*) FROM usuarios");
        return (long)(await comando.ExecuteScalarAsync(cancelamento) ?? 0L);
    }

    /// <summary>
    /// Cadastra um usuário. E-mail repetido é ignorado, não vira erro.
    /// </summary>
    public async Task CadastrarAsync(Usuario usuario, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(Insercao);
        comando.Parameters.AddWithValue("email", Normalizar(usuario.Email));
        comando.Parameters.AddWithValue("nome", usuario.Nome);
        comando.Parameters.AddWithValue("senha_hash", usuario.SenhaHash);
        comando.Parameters.AddWithValue("perfil", usuario.Perfil);

        await comando.ExecuteNonQueryAsync(cancelamento);
    }

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
