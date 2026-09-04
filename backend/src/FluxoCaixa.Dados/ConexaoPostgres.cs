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
        var valor = SemAspasEmVolta(valorDaConfiguracao?.Trim() ?? string.Empty);

        if (valor.Length == 0)
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres está vazia. Defina ConnectionStrings__Postgres "
                + "com a URI do banco (postgresql://usuario:senha@host:porta/banco) ou com a "
                + "string de palavras-chave (Host=...;Port=5432;Database=...;Username=...;Password=...).");
        }

        // Sem o "$" a plataforma nem tenta substituir - e "{{...}}" sozinho é o
        // engano mais fácil de cometer e o mais difícil de enxergar na tela.
        if (valor.StartsWith("{{", StringComparison.Ordinal) && !valor.StartsWith("${{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres começa com \"{{\" e não com \"${{\": falta o \"$\". "
                + "Sem ele a plataforma não substitui a referência e manda o texto cru. "
                + "Escreva ${{NomeDoServico.NOME_DA_VARIAVEL}}.");
        }

        if (valor.Contains("${", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Postgres chegou como referência não resolvida (o texto "
                + "\"${{...}}\" literal, em vez do valor). A plataforma só substitui a referência "
                + "quando o nome do serviço e o da variável existem exatamente como escritos. "
                + "Abra o serviço do banco, veja o nome dele e o nome da variável de conexão, e "
                + "use os dois - ou copie o valor da conexão e cole direto aqui.");
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

    /// <summary>
    /// Colar o valor entre aspas é comum nos editores de variáveis das
    /// plataformas, e ali as aspas viram parte do valor. Não há string de
    /// conexão que comece com aspas, então tirá-las é seguro.
    /// </summary>
    private static string SemAspasEmVolta(string valor) =>
        valor.Length >= 2
        && (valor[0] == '"' || valor[0] == '\'')
        && valor[^1] == valor[0]
            ? valor[1..^1].Trim()
            : valor;

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

    /// <summary>
    /// Descreve o formato do que chegou para dar o que investigar, sem imprimir
    /// o conteúdo: ele carrega a senha do banco e a mensagem vai para o log da
    /// plataforma.
    /// </summary>
    private static string Formato(string texto)
    {
        var esquema = texto.Contains("://", StringComparison.Ordinal)
            ? $"parece uma URI de esquema \"{texto[..texto.IndexOf("://", StringComparison.Ordinal)]}\""
            : texto.Contains('=')
                ? "tem \"=\", então parece palavras-chave"
                : "não tem \"://\" nem \"=\", então não é nenhum dos dois formatos";

        return $"O que chegou tem {texto.Length} caracteres e {esquema}.";
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
                + $"(Host=...;Port=5432;Database=...;Username=...;Password=...). {Formato(texto)} "
                + "Motivo: " + erro.Message,
                erro);
        }
    }
}
