using Microsoft.AspNetCore.Identity;
using Product.Business.Interfaces.Auth;
using Product.Data.Models.Users;

namespace Product.Business.Services.Auth;

public class PasswordHasher : IPasswordHasher, IPasswordHasher<User>
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string hash, string password) => BCrypt.Net.BCrypt.Verify(password, hash);

    string IPasswordHasher<User>.HashPassword(User user, string password) => Hash(password);

    PasswordVerificationResult IPasswordHasher<User>.VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword
    ) =>
        Verify(hashedPassword, providedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
}
