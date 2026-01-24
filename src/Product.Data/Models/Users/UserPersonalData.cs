using Product.Common.Entities;

namespace Product.Data.Models.Users;

public class UserPersonalData : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string? Cpf { get; set; }
    public string? PhoneNumber { get; set; }
    public ApplicationUser? User { get; set; }
    public UserAddress? Address { get; set; }
}
