namespace Application.Auth;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string email,
        string resetLink,
        CancellationToken cancellationToken = default);
}
