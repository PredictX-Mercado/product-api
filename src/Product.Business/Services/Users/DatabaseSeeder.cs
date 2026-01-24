using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Product.Business.Interfaces.Auth;
using Product.Business.Interfaces.Categories;
using Product.Business.Interfaces.Users;
using Product.Common.Enums;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;
using Product.Data.Models.Users.PaymentsMethods;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Users;

public class DatabaseSeeder(
    IDbMigrationRepository migrationRepository,
    IUserRepository userRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IWalletRepository walletRepository,
    IPasswordHasher hasher,
    ICategoryService categoryService,
    IRolePromotionService rolePromotionService
) : IDatabaseSeeder
{
    private readonly IDbMigrationRepository _migrationRepository = migrationRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;
    private readonly IWalletRepository _walletRepository = walletRepository;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly ICategoryService _categoryService = categoryService;
    private readonly IRolePromotionService _rolePromotionService = rolePromotionService;

    private sealed record SeedAddress(
        string ZipCode,
        string Street,
        string Neighborhood,
        string Number,
        string Complement,
        string City,
        string State,
        string Country
    );

    private sealed record SeedBankAccount(
        string BankCode,
        string BankName,
        string Agency,
        string AccountNumber,
        string AccountDigit,
        string AccountType,
        string PixKey
    );

    private sealed record SeedUserData(
        string Email,
        string Username,
        string Name,
        string Password,
        string Cpf,
        string PhoneNumber,
        SeedAddress Address,
        SeedBankAccount BankAccount
    );

    public async Task SeedAsync(IConfiguration configuration, CancellationToken ct = default)
    {
        await _migrationRepository.MigrateAsync(ct);

        // Ensure default categories are seeded
        try
        {
            await _categoryService.EnsureDefaultCategoriesAsync(ct);
        }
        catch
        {
            // swallow category seeding errors
        }

        var adminData = GetAdminSeed();

        var normalizedEmail = NormalizeEmail(adminData.Email);
        var normalizedName = NormalizeName(adminData.Name);

        var admin = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
        if (admin is null)
        {
            var username = string.IsNullOrWhiteSpace(adminData.Username)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(adminData.Username);
            username = await EnsureUniqueUsernameAsync(username, ct);

            admin = new ApplicationUser
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                UserName = username,
                NormalizedUserName = username,
                Name = adminData.Name,
                PasswordHash = _hasher.Hash(adminData.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = adminData.Cpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(adminData.PhoneNumber)
                        ? null
                        : adminData.PhoneNumber,
                    Address = BuildAddressFromSeed(adminData.Address),
                },
            };

            await _userRepository.AddUserAsync(admin, ct);
        }

        await EnsurePaymentMethodsAsync(admin, adminData, ct);

        // Ensure admin has the role string set
        await _rolePromotionService.PromoteToRoleAsync(admin.Id, RoleName.ADMIN_L3.ToString(), ct);

        await SeedDefaultUserAsync(ct);
        await SeedLedgerAsync(ct);
    }

    private async Task SeedDefaultUserAsync(CancellationToken ct)
    {
        var userData = GetUserSeed();

        var normalizedEmail = NormalizeEmail(userData.Email);
        var normalizedName = NormalizeName(userData.Name);

        var user = await _userRepository.GetUserWithPersonalDataByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            var username = string.IsNullOrWhiteSpace(userData.Username)
                ? ExtractUsernameFromEmail(normalizedEmail)
                : NormalizeUsername(userData.Username);
            username = await EnsureUniqueUsernameAsync(username, ct);

            user = new ApplicationUser
            {
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                UserName = username,
                NormalizedUserName = username,
                Name = userData.Name,
                PasswordHash = _hasher.Hash(userData.Password),
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                Status = "ACTIVE",
                PersonalData = new UserPersonalData
                {
                    Cpf = userData.Cpf,
                    PhoneNumber = string.IsNullOrWhiteSpace(userData.PhoneNumber)
                        ? null
                        : userData.PhoneNumber,
                    Address = BuildAddressFromSeed(userData.Address),
                },
            };

            await _userRepository.AddUserAsync(user, ct);
        }

        await EnsurePaymentMethodsAsync(user, userData, ct);

        // Ensure default user has role string set
        await _rolePromotionService.PromoteToRoleAsync(user.Id, RoleName.USER.ToString(), ct);
    }

    private async Task SeedLedgerAsync(CancellationToken ct)
    {
        var enabled = true;
        if (enabled is false)
        {
            return;
        }

        var userEmail = GetUserSeed().Email;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return;
        }

        var normalizedEmail = NormalizeEmail(userEmail);
        var user = await _userRepository.GetUserByEmailAsync(normalizedEmail, ct);
        if (user is null)
        {
            return;
        }

        var accounts = await _walletRepository.EnsureAccountsAsync(user.Id, "BRL", ct);
        var account = accounts[0];

        var paymentKey = $"seed-payment-{user.Id}";
        var withdrawalKey = $"seed-withdrawal-{user.Id}";

        var payment = await _walletRepository.GetPaymentIntentByIdempotencyAsync(
            user.Id,
            paymentKey,
            ct
        );
        if (payment is null)
        {
            payment = new PaymentIntent
            {
                UserId = user.Id,
                Provider = "SEED",
                Amount = 150_000,
                Currency = account.Currency,
                Status = PaymentIntentStatus.APPROVED,
                IdempotencyKey = paymentKey,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            };

            await _walletRepository.AddPaymentIntentAsync(payment, ct);
        }

        var withdrawal = await _walletRepository.GetWithdrawalByIdempotencyAsync(
            user.Id,
            withdrawalKey,
            ct
        );
        if (withdrawal is null)
        {
            withdrawal = new Withdrawal
            {
                UserId = user.Id,
                Amount = 20_000,
                Currency = account.Currency,
                Status = WithdrawalStatus.REQUESTED,
                IdempotencyKey = withdrawalKey,
                Notes = "Seed withdrawal",
            };

            await _walletRepository.AddWithdrawalAsync(withdrawal, ct);
        }

        var baseTime = DateTimeOffset.UtcNow.AddDays(-5);
        var entries = new[]
        {
            new SeedLedgerEntry(
                $"seed-ledger-deposit-{user.Id}",
                LedgerEntryType.DEPOSIT_GATEWAY,
                150_000,
                "PaymentIntent",
                payment.Id,
                baseTime.AddHours(1)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-buy-{user.Id}",
                LedgerEntryType.BET_BUY,
                -50_000,
                "Order",
                null,
                baseTime.AddHours(8)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-payout-{user.Id}",
                LedgerEntryType.PAYOUT,
                80_000,
                "Market",
                null,
                baseTime.AddHours(16)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-fee-{user.Id}",
                LedgerEntryType.FEE,
                -1_000,
                "Fee",
                null,
                baseTime.AddHours(20)
            ),
            new SeedLedgerEntry(
                $"seed-ledger-withdraw-{user.Id}",
                LedgerEntryType.WITHDRAW_REQUEST,
                -20_000,
                "Withdrawal",
                withdrawal.Id,
                baseTime.AddHours(28)
            ),
        };

        var existingKeys = await _walletRepository.GetLedgerEntryIdempotencyKeysAsync(
            account.Id,
            ct
        );

        var toAdd = new List<LedgerEntry>();
        foreach (var entry in entries)
        {
            if (existingKeys.Contains(entry.IdempotencyKey))
            {
                continue;
            }

            toAdd.Add(
                new LedgerEntry
                {
                    AccountId = account.Id,
                    Type = entry.Type,
                    Amount = entry.Amount,
                    ReferenceType = entry.ReferenceType,
                    ReferenceId = entry.ReferenceId,
                    IdempotencyKey = entry.IdempotencyKey,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.CreatedAt,
                }
            );
        }

        if (toAdd.Count > 0)
        {
            await _walletRepository.AddLedgerEntriesAsync(toAdd, ct);
        }
    }

    private async Task<string> EnsureUniqueUsernameAsync(string username, CancellationToken ct)
    {
        var candidate = username;
        var suffix = 1;
        while (await _userRepository.UserNameExistsAsync(candidate, ct))
        {
            candidate = $"{username}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string ExtractUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return NormalizeUsername(email);
        }

        var localPart = email[..atIndex];
        var value = string.IsNullOrWhiteSpace(localPart) ? email : localPart;
        return NormalizeUsername(value);
    }

    private static string NormalizeEmail(string email) =>
        RemoveDiacritics(email).Trim().ToLowerInvariant();

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return RemoveDiacritics(trimmed);
    }

    private static string NormalizeUsername(string username) =>
        RemoveDiacritics(username).Trim().ToLowerInvariant();

    private static string NormalizeRoleName(string roleName) =>
        RemoveDiacritics(roleName).Trim().ToLowerInvariant();

    private static SeedUserData GetAdminSeed()
    {
        return new SeedUserData(
            "admin@product.local",
            "admin",
            "Admin L3",
            "ChangeMe123!",
            "00000000000",
            "11900000000",
            new SeedAddress(
                "00000000",
                "N/A",
                "N/A",
                string.Empty,
                string.Empty,
                "N/A",
                "NA",
                "BR"
            ),
            new SeedBankAccount(
                "001",
                "Banco do Brasil",
                "0001",
                "123456",
                "7",
                "CHECKING",
                "admin@product.local"
            )
        );
    }

    private static SeedUserData GetUserSeed()
    {
        return new SeedUserData(
            "user@product.local",
            "user",
            "User",
            "ChangeMe123!",
            "11111111111",
            "11999999999",
            new SeedAddress(
                "01001000",
                "Rua Exemplo",
                "Centro",
                "100",
                "Apto 12",
                "Sao Paulo",
                "SP",
                "BR"
            ),
            new SeedBankAccount(
                "033",
                "Santander",
                "1234",
                "987654",
                "0",
                "CHECKING",
                "user@product.local"
            )
        );
    }

    private static UserAddress BuildAddressFromSeed(SeedAddress s)
    {
        return new UserAddress
        {
            ZipCode = string.IsNullOrWhiteSpace(s.ZipCode) ? "00000000" : s.ZipCode,
            Street = string.IsNullOrWhiteSpace(s.Street) ? "N/A" : s.Street,
            Neighborhood = string.IsNullOrWhiteSpace(s.Neighborhood) ? null : s.Neighborhood,
            Number = string.IsNullOrWhiteSpace(s.Number) ? null : s.Number,
            Complement = string.IsNullOrWhiteSpace(s.Complement) ? null : s.Complement,
            City = string.IsNullOrWhiteSpace(s.City) ? "N/A" : s.City,
            State = string.IsNullOrWhiteSpace(s.State) ? "NA" : s.State,
            Country = string.IsNullOrWhiteSpace(s.Country) ? "BR" : s.Country,
        };
    }

    private async Task EnsurePaymentMethodsAsync(
        ApplicationUser user,
        SeedUserData seed,
        CancellationToken ct
    )
    {
        var (cardsToAdd, banksToAdd, pixToAdd) = BuildPaymentMethodsFromSeed(seed, user.Id);
        if (cardsToAdd.Count == 0 && banksToAdd.Count == 0 && pixToAdd.Count == 0)
        {
            return;
        }

        var (existingCards, existingBanks, existingPix) =
            await _paymentMethodRepository.GetByUserAsync(user.Id, ct);

        var addedAny = false;

        foreach (var card in cardsToAdd)
        {
            if (existingCards.Any(x => IsSameUserCard(x, card)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserCardAsync(card, ct);
            addedAny = true;
        }

        foreach (var bank in banksToAdd)
        {
            if (existingBanks.Any(x => IsSameUserBank(x, bank)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserBankAccountAsync(bank, ct);
            addedAny = true;
        }

        foreach (var pix in pixToAdd)
        {
            if (existingPix.Any(x => IsSameUserPix(x, pix)))
            {
                continue;
            }

            await _paymentMethodRepository.AddUserPixKeyAsync(pix, ct);
            addedAny = true;
        }

        if (addedAny)
        {
            await _paymentMethodRepository.SaveChangesAsync(ct);
        }
    }

    private static (
        List<UserCard> Cards,
        List<UserBankAccount> Banks,
        List<UserPixKey> Pix
    ) BuildPaymentMethodsFromSeed(SeedUserData seed, Guid userId)
    {
        var cards = new List<UserCard>();
        var banks = new List<UserBankAccount>();
        var pix = new List<UserPixKey>();

        var pixKey = seed.BankAccount.PixKey;
        if (!string.IsNullOrWhiteSpace(pixKey))
        {
            pix.Add(
                new UserPixKey
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PixKey = pixKey,
                    IsDefault = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        if (
            !string.IsNullOrWhiteSpace(seed.BankAccount.BankCode)
            && !string.IsNullOrWhiteSpace(seed.BankAccount.Agency)
            && !string.IsNullOrWhiteSpace(seed.BankAccount.AccountNumber)
        )
        {
            banks.Add(
                new UserBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BankCode = seed.BankAccount.BankCode,
                    BankName = seed.BankAccount.BankName,
                    Agency = seed.BankAccount.Agency,
                    AccountNumber = seed.BankAccount.AccountNumber,
                    AccountDigit = seed.BankAccount.AccountDigit,
                    AccountType = seed.BankAccount.AccountType,
                    IsDefault = pix.Count == 0 && banks.Count == 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        return (cards, banks, pix);
    }

    private static bool IsSameUserCard(UserCard existing, UserCard candidate) =>
        string.Equals(existing.CardLast4, candidate.CardLast4)
        && string.Equals(existing.CardBrand, candidate.CardBrand)
        && existing.CardExpMonth == candidate.CardExpMonth
        && existing.CardExpYear == candidate.CardExpYear;

    private static bool IsSameUserBank(UserBankAccount existing, UserBankAccount candidate) =>
        string.Equals(existing.BankCode, candidate.BankCode)
        && string.Equals(existing.Agency, candidate.Agency)
        && string.Equals(existing.AccountNumber, candidate.AccountNumber)
        && string.Equals(existing.AccountDigit, candidate.AccountDigit);

    private static bool IsSameUserPix(UserPixKey existing, UserPixKey candidate) =>
        string.Equals(existing.PixKey, candidate.PixKey);

    private sealed record SeedLedgerEntry(
        string IdempotencyKey,
        LedgerEntryType Type,
        long Amount,
        string ReferenceType,
        Guid? ReferenceId,
        DateTimeOffset CreatedAt
    );

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
