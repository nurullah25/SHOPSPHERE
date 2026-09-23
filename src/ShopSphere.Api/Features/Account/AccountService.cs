using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Api.Common;
using ShopSphere.Api.Data;
using ShopSphere.Api.Entities;
using ShopSphere.Api.Features.Auth;

namespace ShopSphere.Api.Features.Account;

public class AccountService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountService> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AccountService(AppDbContext db, ILogger<AccountService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProfileDto> GetProfileAsync(int userId)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new ProfileDto
            {
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                CreatedAt = u.CreatedAt
            })
            .SingleOrDefaultAsync()
            ?? throw new NotFoundException("Account not found.");
    }

    public async Task<ProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Account not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        await _db.SaveChangesAsync();
        return await GetProfileAsync(userId);
    }

    // Changing the password signs out other devices but keeps this session,
    // which is what most sites do.
    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, string? currentRefreshToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Account not found.");

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
            throw new BusinessRuleException("Wrong password", "Your current password is not correct.", StatusCodes.Status400BadRequest);

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);

        var keepTokenHash = string.IsNullOrEmpty(currentRefreshToken) ? null : TokenService.HashToken(currentRefreshToken);

        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && (keepTokenHash == null || t.TokenHash != keepTokenHash))
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        await _db.SaveChangesAsync();
        _logger.LogInformation("Password changed for user {UserId}", userId);
    }

    public Task<List<AddressDto>> GetAddressesAsync(int userId) =>
        _db.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a))
            .ToListAsync();

    public async Task<AddressDto> CreateAddressAsync(int userId, AddressRequest request)
    {
        var isFirstAddress = !await _db.Addresses.AnyAsync(a => a.UserId == userId);

        var address = new Address { UserId = userId };
        Apply(address, request);
        address.IsDefault = request.IsDefault || isFirstAddress;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        if (address.IsDefault)
            await ClearDefaultAsync(userId, exceptAddressId: null);

        _db.Addresses.Add(address);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(address);
    }

    public async Task<AddressDto> UpdateAddressAsync(int userId, int addressId, AddressRequest request)
    {
        var address = await _db.Addresses.SingleOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new NotFoundException("Address not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        Apply(address, request);

        if (request.IsDefault)
        {
            // Only one address can be the default, the database enforces it too
            await ClearDefaultAsync(userId, exceptAddressId: addressId);
            address.IsDefault = true;
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToDto(address);
    }

    public async Task DeleteAddressAsync(int userId, int addressId)
    {
        var address = await _db.Addresses.SingleOrDefaultAsync(a => a.Id == addressId && a.UserId == userId)
            ?? throw new NotFoundException("Address not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var wasDefault = address.IsDefault;
        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync();

        if (wasDefault)
        {
            // Promote the newest remaining address so there is always a default
            var replacement = await _db.Addresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (replacement != null)
            {
                replacement.IsDefault = true;
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();
    }

    private Task ClearDefaultAsync(int userId, int? exceptAddressId) =>
        _db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault && (exceptAddressId == null || a.Id != exceptAddressId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsDefault, false));

    private static void Apply(Address address, AddressRequest request)
    {
        address.FullName = request.FullName.Trim();
        address.Line1 = request.Line1.Trim();
        address.Line2 = request.Line2?.Trim();
        address.City = request.City.Trim();
        address.State = request.State?.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
        address.PhoneNumber = request.PhoneNumber?.Trim();
    }

    private static AddressDto ToDto(Address address) => new()
    {
        Id = address.Id,
        FullName = address.FullName,
        Line1 = address.Line1,
        Line2 = address.Line2,
        City = address.City,
        State = address.State,
        PostalCode = address.PostalCode,
        Country = address.Country,
        PhoneNumber = address.PhoneNumber,
        IsDefault = address.IsDefault
    };
}
