using FluxoCaixa.Core.Seguranca;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.Dados;

/// <summary>
/// O primeiro usuário, para o sistema não subir sem ninguém que consiga entrar.
/// </summary>
public sealed record UsuarioInicial(string? Email, string? Senha, string Nome = "Administrador", string Perfil = Perfis.Administrador)
{
    public bool Preenchido => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Senha);
}

public static class CargaInicial
{
    /// <summary>
    /// Cadastra o usuário inicial apenas quando a tabela ainda está vazia.
    /// </summary>
    /// <remarks>
    /// A senha chega em texto puro pela configuração e é convertida em hash
    /// aqui — ela existe só para o primeiro acesso e deve ser trocada, e a
    /// variável de ambiente removida, depois que o sistema estiver no ar.
    /// </remarks>
    public static async Task AplicarAsync(
        RepositorioUsuariosPostgres repositorio,
        UsuarioInicial inicial,
        ILogger logger,
        CancellationToken cancelamento = default)
    {
        if (await repositorio.QuantidadeAsync(cancelamento) > 0)
        {
            return;
        }

        if (!inicial.Preenchido)
        {
            logger.LogWarning(
                "Não há usuários cadastrados e nenhum usuário inicial foi configurado: " +
                "ninguém consegue entrar. Defina UsuarioInicial__Email e UsuarioInicial__Senha.");
            return;
        }

        await repositorio.CadastrarAsync(
            new Usuario(inicial.Email!, inicial.Nome, HashDeSenha.Gerar(inicial.Senha!), inicial.Perfil),
            cancelamento);

        logger.LogInformation("Usuário inicial {Email} cadastrado.", inicial.Email);
    }
}
