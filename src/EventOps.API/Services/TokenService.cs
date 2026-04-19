using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EventOps.Core.Models;
using Microsoft.IdentityModel.Tokens;

namespace EventOps.API.Services;

public class TokenService(IConfiguration config)
{
    public (string Token, DateTime Expira) Generate(Utilizador user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:ExpiresInMinutes"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Perfil.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
