using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace University.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtSettings _jwtSettings;

    // Basit user listesi (hard-coded)
    // İstersen rolleri burada çoğaltırsın
    private static readonly List<(string Username, string Password, string Role)> Users =
    [
        ("admin",    "password", "admin"), // Admin web sitesi
        ("banker", "password",  "bank")   // Banking app
        
    ];

    public AuthController(IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value;
    }

    public record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    public ActionResult<string> Login([FromBody] LoginRequest request)
    {
        // Kullanıcı doğrulama
        var user = Users.SingleOrDefault(u =>
            u.Username == request.Username &&
            u.Password == request.Password);

        if (user == default)
            return Unauthorized();

        // Claims
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role) // ÖNEMLİ: Role claim'i
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:   _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims:   claims,
            expires:  DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(tokenString);
    }
}
