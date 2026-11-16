namespace Application.Auth;

public interface IEmailSender
{
    Task SendVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);
}