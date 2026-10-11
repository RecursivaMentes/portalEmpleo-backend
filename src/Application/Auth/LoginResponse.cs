using Domain.Enums;

namespace Application.Auth;

public record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc
);
