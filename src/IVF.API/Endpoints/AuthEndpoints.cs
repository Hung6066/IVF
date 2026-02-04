using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IVF.API.Contracts;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace IVF.API.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, IUserRepository userRepo, IUnitOfWork uow, IConfiguration config) =>
        {
            var user = await userRepo.GetByUsernameAsync(request.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();
            var token = GenerateJwtToken(user, config);
            var refreshToken = GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();
            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapPost("/refresh", async (RefreshTokenRequest request, IUserRepository userRepo, IUnitOfWork uow, IConfiguration config) =>
        {
            var user = await userRepo.GetByRefreshTokenAsync(request.RefreshToken);
            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow) return Results.Unauthorized();
            var token = GenerateJwtToken(user, config);
            var refreshToken = GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(7));
            await userRepo.UpdateAsync(user);
            await uow.SaveChangesAsync();
            return Results.Ok(new AuthResponse(token, refreshToken, 3600, UserDto.FromEntity(user)));
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, IUserRepository userRepo) =>
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            var user = await userRepo.GetByIdAsync(Guid.Parse(userId));
            return user == null ? Results.NotFound() : Results.Ok(UserDto.FromEntity(user));
        }).RequireAuthorization();
    }

    private static string GenerateJwtToken(User user, IConfiguration config)
    {
        var jwtSettings = config.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.GivenName, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("department", user.Department ?? "")
            }),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
