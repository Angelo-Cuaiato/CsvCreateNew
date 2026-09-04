using System.Text.Json;
using FluxoCaixa.Core.Historico;
using FluxoCaixa.Core.Relatorio;
using Npgsql;
using NpgsqlTypes;

namespace FluxoCaixa.Dados;

/// <summary>
/// Histórico de análises no PostgreSQL.
/// </summary>
/// <remarks>
/// O relatório vai como JSONB e o consolidado como BYTEA. A listagem nunca lê
/// essas duas colunas inteiras: juntas passam de 100 KB por análise, e a tela
/// do histórico só precisa de uma linha por registro.
/// </remarks>
public sealed class RepositorioAnalisesPostgres(NpgsqlDataSource fonte) : IRepositorioAnalises
{
    // O mesmo formato que a API usa na resposta HTTP, para o front receber os
    // campos com os mesmos nomes vindo do histórico ou de uma análise nova.
    private static readonly JsonSerializerOptions Formato = new(JsonSerializerDefaults.Web);

    private const string Insercao = """
        INSERT INTO analises (id, email, autor, nome_arquivo, enviado_em, relatorio, consolidado, origem)
        VALUES (@id, @email, @autor, @nome_arquivo, @enviado_em, @relatorio, @consolidado, @origem)
        """;

    private const string Listagem = """
        SELECT id, email, autor, nome_arquivo, enviado_em,
               relatorio -> 'meses'       AS meses,
               relatorio -> 'conferencia' AS conferencia,
               origem IS NOT NULL          AS tem_origem
          FROM analises
         ORDER BY enviado_em DESC, id DESC
         LIMIT @limite
        """;

    private const string PorId = """
        SELECT id, email, autor, nome_arquivo, enviado_em, relatorio, consolidado, origem
          FROM analises
         WHERE id = @id
        """;

    public bool Persistente => true;

    public async Task GuardarAsync(Analise analise, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(Insercao);
        comando.Parameters.AddWithValue("id", analise.Id);
        comando.Parameters.AddWithValue("email", analise.Email);
        comando.Parameters.AddWithValue("autor", analise.Autor);
        comando.Parameters.AddWithValue("nome_arquivo", analise.NomeArquivo);
        comando.Parameters.AddWithValue("enviado_em", analise.EnviadoEm);
        comando.Parameters.Add(new NpgsqlParameter("relatorio", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(analise.Relatorio, Formato),
        });
        comando.Parameters.AddWithValue("consolidado", analise.Consolidado);
        comando.Parameters.AddWithValue("origem", analise.Origem);

        await comando.ExecuteNonQueryAsync(cancelamento);
    }

    public async Task<IReadOnlyList<ResumoDeAnalise>> ListarAsync(
        int limite = 50, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(Listagem);
        comando.Parameters.AddWithValue("limite", limite);

        var lista = new List<ResumoDeAnalise>();
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        while (await leitor.ReadAsync(cancelamento))
        {
            var meses = JsonSerializer.Deserialize<List<string>>(leitor.GetString(5), Formato) ?? [];
            var conferencia = JsonSerializer.Deserialize<ConferenciaDto>(leitor.GetString(6), Formato);

            lista.Add(new ResumoDeAnalise(
                leitor.GetGuid(0),
                leitor.GetString(1),
                leitor.GetString(2),
                leitor.GetString(3),
                leitor.GetFieldValue<DateTimeOffset>(4),
                meses.Count > 0 ? meses[0] : null,
                meses.Count > 0 ? meses[^1] : null,
                meses.Count,
                conferencia?.Ok ?? false,
                conferencia?.Comparavel ?? false,
                leitor.GetBoolean(7)));
        }

        return lista;
    }

    public async Task<Analise?> PorIdAsync(Guid id, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand(PorId);
        comando.Parameters.AddWithValue("id", id);

        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);
        if (!await leitor.ReadAsync(cancelamento))
        {
            return null;
        }

        var relatorio = JsonSerializer.Deserialize<RelatorioDto>(leitor.GetString(5), Formato);
        if (relatorio is null)
        {
            return null;
        }

        return new Analise(
            leitor.GetGuid(0),
            leitor.GetString(1),
            leitor.GetString(2),
            leitor.GetString(3),
            leitor.GetFieldValue<DateTimeOffset>(4),
            relatorio,
            leitor.GetFieldValue<byte[]>(6),
            leitor.IsDBNull(7) ? [] : leitor.GetFieldValue<byte[]>(7));
    }

    /// <summary>
    /// As análises que podem entrar num somatório: as que têm a planilha de
    /// origem guardada. A ordem é a de envio, para o relatório somado sair na
    /// mesma sequência em que as planilhas chegaram.
    /// </summary>
    public async Task<IReadOnlyList<Analise>> ParaSomarAsync(
        IReadOnlyList<Guid>? ids = null, CancellationToken cancelamento = default)
    {
        var filtrando = ids is { Count: > 0 };

        await using var comando = fonte.CreateCommand($"""
            SELECT id, email, autor, nome_arquivo, enviado_em, relatorio, consolidado, origem
              FROM analises
             WHERE origem IS NOT NULL
               {(filtrando ? "AND id = ANY(@ids)" : string.Empty)}
             ORDER BY enviado_em, id
            """);

        if (filtrando)
        {
            comando.Parameters.AddWithValue("ids", ids!.ToArray());
        }

        var lista = new List<Analise>();
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        while (await leitor.ReadAsync(cancelamento))
        {
            var relatorio = JsonSerializer.Deserialize<RelatorioDto>(leitor.GetString(5), Formato);
            if (relatorio is null)
            {
                continue;
            }

            lista.Add(new Analise(
                leitor.GetGuid(0),
                leitor.GetString(1),
                leitor.GetString(2),
                leitor.GetString(3),
                leitor.GetFieldValue<DateTimeOffset>(4),
                relatorio,
                leitor.GetFieldValue<byte[]>(6),
                leitor.GetFieldValue<byte[]>(7)));
        }

        return lista;
    }

    public async Task<bool> ApagarAsync(Guid id, CancellationToken cancelamento = default)
    {
        await using var comando = fonte.CreateCommand("DELETE FROM analises WHERE id = @id");
        comando.Parameters.AddWithValue("id", id);
        return await comando.ExecuteNonQueryAsync(cancelamento) > 0;
    }
}
