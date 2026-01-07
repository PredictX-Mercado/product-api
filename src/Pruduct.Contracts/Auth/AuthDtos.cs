using System;

namespace Pruduct.Contracts.Auth;

public record SignupRequest(string Name, string Email, string Password, string ConfirmPassword);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record UserPersonalDataView(string Cpf, string? PhoneNumber, string Address);

public record UserView(Guid Id, string Email, string Username, string Name, string[] Roles, UserPersonalDataView? PersonalData);

public record AuthResponse(string AccessToken, string RefreshToken, UserView User);
