using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;

namespace ShopSphere.Api.Features.Auth;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(AppDbContext db, TokenService tokenService, IOptions<JwtSettings> jwtSettings, ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new BusinessRuleException("Email already registered", "An account with this email already exists.");

        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Customer,
            LastLoginAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _db.Users.Add(user);

        var refreshToken = AddRefreshToken(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("New customer registered with id {UserId}", user.Id);
        return BuildResult(user, refreshToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);

        var verification = user == null
            ? PasswordVerificationResult.Failed
            : _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user == null || verification == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid email or password.");

        // Checked after the password so we don't reveal which emails have accounts
        if (!user.IsActive)
            throw new BusinessRuleException("Account disabled", "This account has been disabled. Please contact support.", StatusCodes.Status403Forbidden);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        user.LastLoginAt = DateTime.UtcNow;

        await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync();

        var refreshToken = AddRefreshToken(user);
        await _db.SaveChangesAsync();

        return BuildResult(user, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedException("Refresh token is missing.");

        var hash = TokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash);

        if (stored == null)
            throw new UnauthorizedException("Invalid refresh token.");

        if (stored.RevokedAt != null)
        {
            // A token that was already rotated is being used again. Either it was
            // stolen or replayed, so every session of this user is ended.
            if (stored.ReplacedByTokenHash != null)
            {
                _logger.LogWarning("Refresh token reuse detected for user {UserId}, revoking all sessions", stored.UserId);
                await _db.RefreshTokens
                    .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
            }

            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        if (!stored.User.IsActive)
            throw new UnauthorizedException("This account has been disabled.");

        var newToken = AddRefreshToken(stored.User);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = newToken.Entity.TokenHash;

        await _db.SaveChangesAsync();

        return BuildResult(stored.User, newToken);
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        var hash = TokenService.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);

        if (stored != null && stored.RevokedAt == null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("User not found.");

        return ToDto(user);
    }

    private (string Token, RefreshToken Entity) AddRefreshToken(User user)
    {
        var token = TokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            User = user,
            TokenHash = TokenService.HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(entity);
        return (token, entity);
    }

    private AuthResult BuildResult(User user, (string Token, RefreshToken Entity) refreshToken)
    {
        var (accessToken, expiresAt) = _tokenService.CreateAccessToken(user);

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            User = ToDto(user)
        };

        return new AuthResult(response, refreshToken.Token, refreshToken.Entity.ExpiresAt);
    }

    private static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Role = user.Role.ToString()
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
