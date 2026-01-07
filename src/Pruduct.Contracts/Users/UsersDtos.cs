namespace Pruduct.Contracts.Users;

public record UpdateMeRequest(
	string Name,
	string Email,
	string? Password,
	string? ConfirmPassword
);
