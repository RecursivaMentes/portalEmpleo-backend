namespace Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password) &&
        password.Length >= MinLength && password.Length <= MaxLength;
}
