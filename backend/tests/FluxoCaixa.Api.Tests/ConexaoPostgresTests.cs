using FluxoCaixa.Dados;
using Npgsql;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// A conexão com o banco vem de onde a aplicação estiver rodando: do compose,
/// em palavras-chave, ou de uma plataforma de deploy, em URI. As duas precisam
/// chegar iguais no Npgsql — e o que não der para usar precisa dizer por quê.
/// </summary>
public class ConexaoPostgresTests
{
    private static NpgsqlConnectionStringBuilder Ler(string valor) => new(ConexaoPostgres.Normalizar(valor));

    [Fact]
    public void Aceita_a_string_de_palavras_chave_do_compose()
    {
        var conexao = Ler("Host=db;Port=5432;Database=fluxocaixa;Username=fluxo;Password=segredo");

        Assert.Equal("db", conexao.Host);
        Assert.Equal(5432, conexao.Port);
        Assert.Equal("fluxocaixa", conexao.Database);
        Assert.Equal("fluxo", conexao.Username);
        Assert.Equal("segredo", conexao.Password);
    }

    [Theory]
    [InlineData("postgresql://fluxo:segredo@postgres.railway.internal:5432/railway")]
    [InlineData("postgres://fluxo:segredo@postgres.railway.internal:5432/railway")]
    public void Aceita_a_uri_que_as_plataformas_entregam(string uri)
    {
        var conexao = Ler(uri);

        Assert.Equal("postgres.railway.internal", conexao.Host);
        Assert.Equal(5432, conexao.Port);
        Assert.Equal("railway", conexao.Database);
        Assert.Equal("fluxo", conexao.Username);
        Assert.Equal("segredo", conexao.Password);
    }

    [Fact]
    public void Usa_a_porta_padrao_quando_a_uri_nao_traz_uma()
    {
        Assert.Equal(5432, Ler("postgresql://fluxo:segredo@banco.interno/railway").Port);
    }

    [Fact]
    public void Desfaz_o_escape_da_senha_vinda_na_uri()
    {
        // Senha gerada por plataforma costuma ter "@", "/" e "+", que chegam
        // percent-encoded e não valem nada se forem repassados assim.
        var conexao = Ler("postgresql://fluxo:a%40b%2Fc%2Bd@banco.interno:5432/railway");

        Assert.Equal("a@b/c+d", conexao.Password);
    }

    [Fact]
    public void Traduz_o_sslmode_da_uri_para_o_nome_que_o_npgsql_usa()
    {
        var conexao = Ler("postgresql://fluxo:segredo@banco.interno:5432/railway?sslmode=require");

        Assert.Equal(SslMode.Require, conexao.SslMode);
    }

    [Fact]
    public void Explica_quando_as_referencias_nao_resolveram_e_o_host_veio_vazio()
    {
        // O engano real: "Host=${{Postgres.PGHOST}};..." com o serviço chamado
        // de outro jeito vira isto, e o Npgsql só dizia "Host can't be null".
        var erro = Assert.Throws<InvalidOperationException>(
            () => ConexaoPostgres.Normalizar("Host=;Port=;Database=;Username=;Password="));

        Assert.Contains("sem host", erro.Message);
        Assert.Contains("não resolveram", erro.Message);
    }

    [Fact]
    public void Explica_quando_a_variavel_nem_foi_definida()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ConexaoPostgres.Normalizar("   "));

        Assert.Contains("ConnectionStrings__Postgres", erro.Message);
    }

    [Fact]
    public void Explica_quando_o_formato_nao_e_nenhum_dos_dois()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => ConexaoPostgres.Normalizar("mysql://fluxo:segredo@banco.interno/railway"));

        Assert.Contains("formato", erro.Message);
    }

    [Fact]
    public void Nao_repete_a_senha_na_mensagem_de_erro()
    {
        // A mensagem vai para o log da plataforma; a senha do banco não pode ir junto.
        var erro = Assert.Throws<InvalidOperationException>(
            () => ConexaoPostgres.Normalizar("isto=nao;e=conexao;senha=nao-pode-vazar"));

        Assert.DoesNotContain("nao-pode-vazar", erro.Message);
    }
}
