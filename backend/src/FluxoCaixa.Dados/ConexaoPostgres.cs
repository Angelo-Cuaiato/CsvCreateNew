using Npgsql;

namespace FluxoCaixa.Dados;

/// <summary>
/// Traduz o que veio na configuração para uma string que o Npgsql entende.
///
/// O Npgsql só aceita o formato de palavras-chave
/// (<c>Host=...;Port=...;Database=...</c>), mas quase toda plataforma de deploy
/// entrega a conexão como URI (<c>postgresql://usuario:senha@host:porta/banco</c>),
/// numa variável só. Exigir que a pessoa desmonte essa URI em cinco pedaços é
/// pedir erro: basta um pedaço não resolver para a API subir com
/// <c>Host=</c> vazio e morrer com "Host can't be null", que não diz o que fazer.
/// </summary>
public static class ConexaoPostgres
{
    /// <summary>Devolve a string pronta para o <c>NpgsqlDataSource</c>.</summary>
    /// <exception cref="InvalidOperationException">
    /// Quando o valor não dá para usar — com o motivo, sem repetir a senha.
    /// </exception>
    public static string Normalizar(string? valorDaConfiguracao)
    {
        var valor = valorDaConfiguracao?.Trim() ?? string.Empty;

        if (valor.Length == 0)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres está vazia. Defina ConnectionStrings__Postgres "
                + "com a URI do banco (postgresql://usuario:senha@host:porta/banco) ou com a "
                + "string de palavras-chave (Host=...;Port=5432;Database=...;Username=...;Password=...).");
        }

        var texto = EhUri(valor) ? DeUriParaPalavrasChave(valor) : valor;

        var construtor = LerOuExplicar(texto);

        if (string.IsNullOrWhiteSpace(construtor.Host))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres chegou sem host. Isso acontece quando a variável "
                + "foi montada com referências que não resolveram e o valor virou algo como "
                + "\"Host=;Port=;Database=\". Confira o nome do serviço do banco na plataforma, "
                + "ou aponte direto para a URI que ela oferece pronta.");
        }

        return construtor.ConnectionString;
    }

    private static bool EhUri(string valor) =>
        valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Desmonta a URI. O usuário e a senha vêm percent-encoded — uma senha com
    /// "@" ou "/" chega escapada e precisa voltar ao original antes de virar
    /// palavra-chave.
    /// </summary>
    private static string DeUriParaPalavrasChave(string valor)
    {
        Uri uri;

        try
        {
            uri = new Uri(valor);
        }
        catch (UriFormatException erro)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres parece uma URI, mas não é uma URI válida: "
                + erro.Message, erro);
        }

        var construtor = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            // Sem porta na URI, o padrão do PostgreSQL.
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var credenciais = uri.UserInfo.Split(':', 2);

        if (credenciais[0].Length > 0)
        {
            construtor.Username = Uri.UnescapeDataString(credenciais[0]);
        }

        if (credenciais.Length == 2)
        {
            construtor.Password = Uri.UnescapeDataString(credenciais[1]);
        }

        // A URI costuma trazer sslmode=require na query; o Npgsql tem a mesma
        // opção com outro nome. Os demais parâmetros seguem adiante como estão.
        foreach (var parametro in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = parametro.Split('=', 2);

            if (partes.Length != 2)
            {
                continue;
            }

            var nome = Uri.UnescapeDataString(partes[0]);
            var conteudo = Uri.UnescapeDataString(partes[1]);

            construtor[nome.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ? "SSL Mode" : nome] = conteudo;
        }

        return construtor.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder LerOuExplicar(string texto)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(texto);
        }
        catch (Exception erro) when (erro is ArgumentException or FormatException)
        {
            // Sem repetir o valor: ele carrega a senha do banco e isto vai para
            // o log da plataforma.
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres não está num formato que o Npgsql entenda. "
                + "Use a URI (postgresql://usuario:senha@host:porta/banco) ou as palavras-chave "
                + "(Host=...;Port=5432;Database=...;Username=...;Password=...). Motivo: " + erro.Message,
                erro);
        }
    }
}
