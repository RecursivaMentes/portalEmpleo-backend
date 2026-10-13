using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record LoginRequest(
    [property: Required, EmailAddress, MaxLength(256)]
    string Email,

    [property: Required]
    string Password
);
