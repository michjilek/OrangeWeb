namespace Op_LP.Services.Admin;

public interface IAdminCredentialValidator
{
    Task<AdminCredentialValidationResult> ValidateCredentialsAsync(string? username, string? password, CancellationToken cancellationToken = default);
}
