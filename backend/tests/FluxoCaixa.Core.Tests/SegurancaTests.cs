using FluxoCaixa.Core.Seguranca;

namespace FluxoCaixa.Core.Tests;

public class HashDeSenhaTests
{
    [Fact]
    public void Gerar_NaoGuardaASenhaEmTextoPuro()
    {
        var hash = HashDeSenha.Gerar("segredo-do-usuario");

        Assert.DoesNotContain("segredo-do-usuario", hash);
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.Equal(4, hash.Split('$').Length);
    }

    [Fact]
    public void Gerar_UsaSaltDiferenteACadaChamada()
    {
        var primeiro = HashDeSenha.Gerar("mesma-senha");
        var segundo = HashDeSenha.Gerar("mesma-senha");

        Assert.NotEqual(primeiro, segundo);
        Assert.True(HashDeSenha.Conferir("mesma-senha", primeiro));
        Assert.True(HashDeSenha.Conferir("mesma-senha", segundo));
    }

    [Fact]
    public void Conferir_AceitaASenhaCerta()
        => Assert.True(HashDeSenha.Conferir("Fluxo@2026", HashDeSenha.Gerar("Fluxo@2026")));

    [Theory]
    [InlineData("outra-senha")]
    [InlineData("Fluxo@2026 ")]
    [InlineData("fluxo@2026")]
    [InlineData("")]
    [InlineData(null)]
    public void Conferir_RecusaQualquerCoisaDiferente(string? tentativa)
        => Assert.False(HashDeSenha.Conferir(tentativa, HashDeSenha.Gerar("Fluxo@2026")));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("texto-solto")]
    [InlineData("bcrypt$210000$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$naoehnumero$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$210000$nao-e-base64!$aGFzaA==")]
    public void Conferir_HashMalformadoNaoEstoura(string? armazenado)
        => Assert.False(HashDeSenha.Conferir("qualquer", armazenado));

    [Fact]
    public void Gerar_ExigeSenha()
        => Assert.Throws<ArgumentException>(() => HashDeSenha.Gerar("   "));
}

public class ServicoAutenticacaoTests
{
    private const string Senha = "Fluxo@2026";

    private static ServicoAutenticacao Servico() => new(new RepositorioUsuariosEmMemoria(
    [
        new Usuario("admin@exemplo.com", "Administrador", HashDeSenha.Gerar(Senha), Perfis.Administrador),
        new Usuario("ana@exemplo.com", "Ana", HashDeSenha.Gerar("outra-senha")),
    ]));

    [Fact]
    public void Autenticar_DevolveOUsuarioQuandoAsCredenciaisConferem()
    {
        var usuario = Servico().Autenticar("admin@exemplo.com", Senha);

        Assert.NotNull(usuario);
        Assert.Equal("Administrador", usuario.Nome);
        Assert.Equal(Perfis.Administrador, usuario.Perfil);
    }

    [Theory]
    [InlineData("ADMIN@exemplo.com")]
    [InlineData("  admin@exemplo.com  ")]
    public void Autenticar_IgnoraCaixaEEspacosNoEmail(string email)
        => Assert.NotNull(Servico().Autenticar(email, Senha));

    [Theory]
    [InlineData("admin@exemplo.com", "senha-errada")]
    [InlineData("naoexiste@exemplo.com", Senha)]
    [InlineData("", Senha)]
    [InlineData(null, Senha)]
    [InlineData("admin@exemplo.com", null)]
    public void Autenticar_RecusaCredenciaisInvalidas(string? email, string? senha)
        => Assert.Null(Servico().Autenticar(email, senha));

    [Fact]
    public void Autenticar_NaoAceitaASenhaDeOutroUsuario()
        => Assert.Null(Servico().Autenticar("ana@exemplo.com", Senha));
}

public class RepositorioUsuariosEmMemoriaTests
{
    [Fact]
    public void PorEmail_EncontraIgnorandoCaixa()
    {
        var repositorio = new RepositorioUsuariosEmMemoria(
            [new Usuario("Ana@Exemplo.com", "Ana", "hash")]);

        Assert.NotNull(repositorio.PorEmail("ana@exemplo.com"));
        Assert.Null(repositorio.PorEmail("outro@exemplo.com"));
    }

    [Fact]
    public void EmailRepetidoNaoQuebraACarga()
    {
        var repositorio = new RepositorioUsuariosEmMemoria(
        [
            new Usuario("ana@exemplo.com", "Ana", "hash-antigo"),
            new Usuario("ana@exemplo.com", "Ana Maria", "hash-novo"),
        ]);

        Assert.Equal(1, repositorio.Quantidade);
        Assert.Equal("Ana Maria", repositorio.PorEmail("ana@exemplo.com")!.Nome);
    }
}
