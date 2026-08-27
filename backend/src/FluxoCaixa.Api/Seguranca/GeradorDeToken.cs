using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluxoCaixa.Core.Seguranca;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FluxoCaixa.Api.Seguranca;

/// <summary>Token emitido no login.</summary>
public sealed record TokenEmitido(string Token, DateTime ExpiraEm, string Email, string Nome, string Perfil);

/// <summary>
/// Assina o JWT que o front usa nas chamadas seguintes.
/// </summary>
public sealed class GeradorDeToken(IOptions<OpcoesJwt> opcoes)
{
    private readonly OpcoesJwt _opcoes = opcoes.Value;

    public TokenEmitido Emitir(Usuario usuario)
    {
        var agora = DateTime.UtcNow;
        var expiraEm = agora.AddMinutes(_opcoes.MinutosDeValidade);

        var identificacao = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Perfil),
        };

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: identificacao,
            notBefore: agora,
            expires: expiraEm,
            signingCredentials: new SigningCredentials(_opcoes.Chave(), SecurityAlgorithms.HmacSha256));

        return new TokenEmitido(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiraEm,
            usuario.Email,
            usuario.Nome,
            usuario.Perfil);
    }
}
