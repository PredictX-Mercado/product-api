using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Abstractions;
using Pruduct.Business.Abstractions.Results;
using Pruduct.Contracts.Auth;
using Pruduct.Contracts.Users;
using Pruduct.Data.Database.Contexts;

namespace Pruduct.Business.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ServiceResult<UserView>> GetMeAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        var roles = await _db
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleName, r => r.Name, (ur, r) => r.Name.ToString())
            .ToArrayAsync(ct);

        var personal = user.PersonalData;
        var view = new UserView(
            user.Id,
            user.Email,
            user.Username,
            user.Name,
            roles,
            personal is null
                ? null
                : new UserPersonalDataView(personal.Cpf, personal.PhoneNumber, personal.Address)
        );

        return ServiceResult<UserView>.Ok(view);
    }

    public async Task<ServiceResult<UserView>> UpdateMeAsync(
        Guid userId,
        UpdateMeRequest request,
        CancellationToken ct = default
    )
    {
        var user = await _db
            .Users.Include(u => u.PersonalData)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return ServiceResult<UserView>.Fail("user_not_found");
        }

        var emailTaken = await _db.Users.AnyAsync(
            u => u.Id != userId && u.Email == request.Email,
            ct
        );
        if (emailTaken)
        {
            return ServiceResult<UserView>.Fail("email_taken");
        }

        user.Name = request.Name;
        user.Email = request.Email;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
        }

        await _db.SaveChangesAsync(ct);

        var roles = await _db
            .UserRoles.Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleName, r => r.Name, (ur, r) => r.Name.ToString())
            .ToArrayAsync(ct);

        var personal = user.PersonalData;
        var view = new UserView(
            user.Id,
            user.Email,
            user.Username,
            user.Name,
            roles,
            personal is null
                ? null
                : new UserPersonalDataView(personal.Cpf, personal.PhoneNumber, personal.Address)
        );

        return ServiceResult<UserView>.Ok(view);
    }
}
