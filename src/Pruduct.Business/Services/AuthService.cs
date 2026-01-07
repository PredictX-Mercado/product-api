using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Abstractions;
using Pruduct.Business.Abstractions.Results;
using Pruduct.Business.Options;
using Pruduct.Common.Enums;
using Pruduct.Contracts.Auth;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models;

namespace Pruduct.Business.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        Microsoft.Extensions.Options.IOptions<JwtOptions> jwtOptions
    )
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<ServiceResult<AuthResponse>> SignupAsync(
        SignupRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var emailExists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (emailExists)
        {
            return ServiceResult<AuthResponse>.Fail("email_already_registered");
        }

        var username = ExtractUsernameFromEmail(normalizedEmail);
        var normalizedName = NormalizeName(request.Name);

        var user = new User
        {
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            Username = username,
            NormalizedUsername = username,
            Name = request.Name,
            NormalizedName = normalizedName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = "ACTIVE",
            PersonalData = null,
        };

        var userRole = await EnsureRoleAsync(RoleName.USER, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
        await _db.SaveChangesAsync(ct);

        var tokens = await IssueTokensAsync(user, ct);

        await tx.CommitAsync(ct);

        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    )
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_credentials");
        }

        var validPassword = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (!validPassword)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_credentials");
        }

        if (!string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<AuthResponse>.Fail("user_inactive");
        }

        var tokens = await IssueTokensAsync(user, ct);
        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(
        RefreshRequest request,
        CancellationToken ct = default
    )
    {
        var hashed = _tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hashed, ct);
        if (existing is null)
        {
            return ServiceResult<AuthResponse>.Fail("invalid_refresh_token");
        }

        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ServiceResult<AuthResponse>.Fail("expired_refresh_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, ct);
        if (user is null)
        {
            return ServiceResult<AuthResponse>.Fail("user_not_found");
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var tokens = await IssueTokensAsync(user, ct);
        return ServiceResult<AuthResponse>.Ok(tokens);
    }

    private async Task<Role> EnsureRoleAsync(RoleName roleName, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is not null)
            return role;

        role = new Role { Name = roleName };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return role;
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var roles = await _db
            .UserRoles.Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles, ur => ur.RoleName, r => r.Name, (ur, r) => r.Name)
            .ToListAsync(ct);

        if (roles.Count == 0)
        {
            var userRole = await EnsureRoleAsync(RoleName.USER, ct);
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = userRole.Name });
            await _db.SaveChangesAsync(ct);
            roles.Add(userRole.Name);
        }

        var subject = new TokenSubject(user.Id, user.Email, user.Username, user.Name);

        var roleStrings = roles.Select(r => r.ToString()).ToArray();

        var accessToken = _tokenService.GenerateAccessToken(subject, roleStrings);
        var refreshRaw = _tokenService.GenerateRefreshToken();
        var refreshHash = _tokenService.HashRefreshToken(refreshRaw);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        var personal = user.PersonalData;

        var userView = new UserView(
            user.Id,
            user.Email,
            user.Username,
            user.Name,
            roleStrings,
            personal is null
                ? null
                : new UserPersonalDataView(personal.Cpf, personal.PhoneNumber, personal.Address)
        );

        return new AuthResponse(accessToken, refreshRaw, userView);
    }

    private static string ExtractUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return email;
        }

        var localPart = email[..atIndex];
        var value = string.IsNullOrWhiteSpace(localPart) ? email : localPart;
        return RemoveDiacritics(value).ToLowerInvariant();
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var idx = 0;

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                buffer[idx++] = c;
            }
        }

        return new string(buffer[..idx]).Normalize(NormalizationForm.FormC);
    }
}
