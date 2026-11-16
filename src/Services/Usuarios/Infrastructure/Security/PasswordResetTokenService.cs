using System.Security.Cryptography;
using System.Text;
using Application.Auth;
using Microsoft.AspNetCore.DataProtection;

namespace Usuarios.Infrastructure.Security;

public class PasswordResetTokenService : IPasswordResetTokenService
{
    private readonly ITimeLimitedDataProtector _protector;

    public PasswordResetTokenService(
        IDataProtectionProvider provider)
    {
        _protector = provider
            .CreateProtector("PortalEmpleo.PasswordReset.v1")
            .ToTimeLimitedDataProtector();
    }

    public string GenerateToken(Guid userId, string passwordHash)
    {
        var passwordFingerprint = GetFingerprint(passwordHash);

        var payload = $"{userId:N}:{passwordFingerprint}";

        return _protector.Protect(
            payload,
            TimeSpan.FromMinutes(15));
    }

    public bool ValidateToken(
        string token,
        Guid userId,
        string passwordHash)
    {
        try
        {
            var payload = _protector.Unprotect(token);

            var expectedPayload =
                $"{userId:N}:{GetFingerprint(passwordHash)}";

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(payload),
                Encoding.UTF8.GetBytes(expectedPayload));
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetFingerprint(string passwordHash)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(passwordHash));

        return Convert.ToHexString(bytes);
    }
}
