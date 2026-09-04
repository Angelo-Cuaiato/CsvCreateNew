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

    public bool Persistente => true;

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

    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand("""
            SELECT email, nome, senha_hash, perfil
              FROM usuarios
             ORDER BY email
            """);

        var lista = new List<Usuario>();
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        while (await leitor.ReadAsync(cancelamento))
        {
            lista.Add(new Usuario(
                leitor.GetString(0),
                leitor.GetString(1),
                leitor.GetString(2),
                leitor.GetString(3)));
        }

        return lista;
    }

    /// <summary>
    /// Cadastra um usuário novo. E-mail repetido devolve false, e não erro: é
    /// resposta de tela, não falha de sistema.
    /// </summary>
    public async Task<bool> CriarAsync(Usuario usuario, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(Insercao);
        comando.Parameters.AddWithValue("email", Normalizar(usuario.Email));
        comando.Parameters.AddWithValue("nome", usuario.Nome);
        comando.Parameters.AddWithValue("senha_hash", usuario.SenhaHash);
        comando.Parameters.AddWithValue("perfil", usuario.Perfil);

        return await comando.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<bool> TrocarSenhaAsync(
        string email, string senhaHash, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(
            "UPDATE usuarios SET senha_hash = @senha_hash WHERE lower(email) = @email");

        comando.Parameters.AddWithValue("email", Normalizar(email));
        comando.Parameters.AddWithValue("senha_hash", senhaHash);

        return await comando.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<bool> ApagarAsync(string email, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand("DELETE FROM usuarios WHERE lower(email) = @email");
        comando.Parameters.AddWithValue("email", Normalizar(email));

        return await comando.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Quantos usuários existem — usado para decidir a carga inicial.</summary>
    public async Task<long> QuantidadeAsync(CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand("SELECT count(*) FROM usuarios");
        return (long)(await comando.ExecuteScalarAsync(cancelamento) ?? 0L);
    }

    private static string Normalizar(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
