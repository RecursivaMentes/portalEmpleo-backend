namespace Application.Auth;

public interface IPasswordResetTokenService
{
    string GenerateToken(Guid userId, string passwordHash);

    bool ValidateToken(
        string token,
        Guid userId,
        string passwordHash);
}
