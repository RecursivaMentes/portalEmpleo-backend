using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record LoginRequest(
    [param: Required, EmailAddress, MaxLength(256)]
    string Email,

    [param: Required]
    string Password
);