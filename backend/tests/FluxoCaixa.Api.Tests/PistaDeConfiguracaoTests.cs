using FluxoCaixa.Api.Seguranca;

namespace FluxoCaixa.Api.Tests;

/// <summary>
/// A mensagem que aparece quando a API sobe sem a chave do token. Ela é lida no
/// log de uma plataforma de deploy, onde ninguém tem shell no contêiner: por
/// isso precisa dizer o que chegou de fato — e nunca o valor de nada.
/// </summary>
public class PistaDeConfiguracaoTests
{
    [Fact]
    public void Aponta_o_nome_do_compose_quando_ele_e_o_que_esta_definido()
    {
        var pista = OpcoesJwt.PistaSobreOAmbiente(["PATH", "JWT_CHAVE", "JWT_MINUTOS"]);

        Assert.Contains("JWT_CHAVE", pista);
        Assert.Contains("docker compose", pista);
        Assert.Contains("Jwt__ChaveSecreta", pista);
    }

    [Fact]
    public void Lista_as_parecidas_quando_ha_alguma_mas_nao_a_certa()
    {
        var pista = OpcoesJwt.PistaSobreOAmbiente(["PATH", "Jwt__MinutosDeValidade"]);

        Assert.Contains("Jwt__MinutosDeValidade", pista);
        Assert.Contains("Nenhuma delas é Jwt__ChaveSecreta", pista);
    }

    [Fact]
    public void Diz_que_nada_chegou_quando_nenhuma_variavel_do_token_veio()
    {
        var pista = OpcoesJwt.PistaSobreOAmbiente(["PATH", "HOME", "PORT"]);

        Assert.Contains("Nenhuma variável", pista);
        Assert.Contains("(das 3 presentes)", pista);
        Assert.Contains("deploy foi aplicado", pista);
    }

    [Fact]
    public void Nunca_repete_o_valor_de_uma_variavel()
    {
        // Só nomes entram e só nomes saem: a mensagem vai para o log da plataforma.
        var pista = OpcoesJwt.PistaSobreOAmbiente(["Jwt__Emissor"]);

        Assert.DoesNotContain("=", pista);
    }

    [Fact]
    public void A_excecao_da_subida_ensina_o_nome_certo()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => new OpcoesJwt().Validar());

        Assert.Contains("Jwt__ChaveSecreta", erro.Message);
        Assert.Contains("dois sublinhados", erro.Message);
    }
}
