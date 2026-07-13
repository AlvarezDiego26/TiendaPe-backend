using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TiendaPe.Api;
using TiendaPe.Domain.Entities;
using TiendaPe.Infrastructure.Persistence;

namespace TiendaPe.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(TiendaPeDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("bootstrap")]
    public async Task<ActionResult<AuthResponse>> Bootstrap(BootstrapRequest request, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return Conflict("Ya existe un usuario inicial.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email y contraseña son obligatorios.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(CreateAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Credenciales inválidas.");
        }

        return Ok(CreateAuthResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(12);
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = "tiendape-development-secret-change-before-production-2026";
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "TiendaPe",
            audience: configuration["Jwt:Audience"] ?? "TiendaPe",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse(
            user.Id,
            user.FullName,
            user.Email,
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
