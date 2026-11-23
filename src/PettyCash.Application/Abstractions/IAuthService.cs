using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> SendResetCodeAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse> SignInWithMicrosoftAsync(MicrosoftSessionRequest request, CancellationToken cancellationToken = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<MeResponse?> SetEmojiAsync(string userId, string? emoji, CancellationToken cancellationToken = default);
}
