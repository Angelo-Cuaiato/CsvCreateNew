using Microsoft.Extensions.Logging;
using Npgsql;

namespace FluxoCaixa.Dados;

/// <summary>
/// Cria o que o banco precisa ter na subida da aplicação.
/// </summary>
/// <remarks>
/// O script é idempotente (<c>IF NOT EXISTS</c>), então rodar de novo não
/// quebra nada — é o que permite subir vários contêineres da API contra o
/// mesmo banco sem coordenação.
/// </remarks>
public static class EsquemaDoBanco
{
    private const string Script = """
        CREATE TABLE IF NOT EXISTS usuarios (
            email       TEXT PRIMARY KEY,
            nome        TEXT NOT NULL,
            senha_hash  TEXT NOT NULL,
            perfil      TEXT NOT NULL DEFAULT 'usuario',
            criado_em   TIMESTAMPTZ NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS analises (
            id            UUID PRIMARY KEY,
            email         TEXT NOT NULL,
            autor         TEXT NOT NULL,
            nome_arquivo  TEXT NOT NULL,
            enviado_em    TIMESTAMPTZ NOT NULL,
            relatorio     JSONB NOT NULL,
            consolidado   BYTEA NOT NULL
        );

        -- Guardada para o somatório de várias análises, que precisa refazer a
        -- hierarquia sobre os valores somados. Aceita nulo porque análises
        -- gravadas antes desta coluna não têm origem - elas ficam de fora da soma.
        ALTER TABLE analises ADD COLUMN IF NOT EXISTS origem BYTEA;

        -- A lista do histórico é sempre "as mais recentes primeiro".
        CREATE INDEX IF NOT EXISTS analises_enviado_em ON analises (enviado_em DESC);
        """;

    /// <summary>
    /// Espera o banco aceitar conexão e aplica o esquema.
    /// </summary>
    /// <remarks>
    /// A espera existe porque em <c>docker compose</c> a API costuma subir
    /// antes de o Postgres terminar de abrir a porta.
    /// </remarks>
    public static async Task PrepararAsync(
        NpgsqlDataSource fonte,
        ILogger logger,
        int tentativas = 10,
        TimeSpan? intervalo = null,
        CancellationToken cancelamento = default)
    {
        intervalo ??= TimeSpan.FromSeconds(2);

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var comando = fonte.CreateCommand(Script);
                await comando.ExecuteNonQueryAsync(cancelamento);
                logger.LogInformation("Esquema do banco conferido.");
                return;
            }
            catch (NpgsqlException erro) when (tentativa < tentativas)
            {
                logger.LogWarning(
                    "Banco ainda não respondeu ({Tentativa}/{Tentativas}): {Motivo}. Tentando de novo em {Segundos}s.",
                    tentativa, tentativas, erro.Message, intervalo.Value.TotalSeconds);

                await Task.Delay(intervalo.Value, cancelamento);
            }
        }
    }
}
