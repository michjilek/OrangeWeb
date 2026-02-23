using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;

namespace Op_LP.Services.Admin;

public sealed class AdminCredentialValidator : IAdminCredentialValidator
{
    #region Dependency Injection
    // Options = Username, Hash, Salt, Iteration Count
    private IOptions<AdminAuthOptions> _options;
    //private readonly ILogger<AdminCredentialValidator> _logger;
    private ICustomLogger _customLogger;
    #endregion

    #region Ctor
    public AdminCredentialValidator(IOptions<AdminAuthOptions> options,
                                    ICustomLogger customLogger
                                    //ILogger<AdminCredentialValidator> logger
                                    )
    {
        _options = options;
        //_logger = logger;
        _customLogger = customLogger;
    }
    #endregion

    #region Public Methods
    public Task<AdminCredentialValidationResult> ValidateCredentialsAsync(string? username,
                                                                          string? password,
                                                                          CancellationToken cancellationToken = default)
    {
        // When something cancel job
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AdminCredentialValidationResult>(cancellationToken);
        }

        // If _options.Value (AdminAuthOptions) is null, create new one
        var settings = _options.Value ?? new AdminAuthOptions();

        // If properties in settings are empty, return not configured result (result = not succeeded, not configured, no user)
        if (!IsConfigured(settings))
        {
            return Task.FromResult(AdminCredentialValidationResult.NotConfigured());
        }

        // If settings are ok...
        // check username and password.... but when issue, return invalid credential result (result = not succeeded, configured, no user)
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return Task.FromResult(AdminCredentialValidationResult.InvalidCredentials());
        }

        // Id setting is ok and username and password are not empty...
        try
        {
            // Canonical is standartized user name (admin is Admin, ADMIN, admin@example.com ect.)
            // ! means - i know, that it is not null
            var canonicalUsername = settings.Username!.Trim();

            // If username is not username from app settings, return invalid credentials result = not succeeded, configured, no user)
            if (!string.Equals(username.Trim(), canonicalUsername, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AdminCredentialValidationResult.InvalidCredentials());
            }

            // Now we have right username...
            // Convert salt and hash to bytes
            var saltBytes = Convert.FromBase64String(settings.PasswordSalt!);
            var expectedHash = Convert.FromBase64String(settings.PasswordHash!);

            // If we have no bytes...
            if (saltBytes.Length == 0 || expectedHash.Length == 0)
            {
                _customLogger.MyLogger.Warning($"AdminCredentialValidator: Administrator credentials are misconfigured. Salt or hash is empty.");
                return Task.FromResult(AdminCredentialValidationResult.NotConfigured());
            }

            // Get iteration count
            var iterationCount = settings.IterationCount > 0
                ? settings.IterationCount
                : AdminAuthOptions.DefaultIterationCount;

            // Derivation of key, it must be same byte length (result is byte[])
            var derived = KeyDerivation.Pbkdf2(
                password: password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: iterationCount,
                numBytesRequested: expectedHash.Length);

            // Here compare derived hash (from user) and expectedHash (hash from app settings)
            // and retunr result
            return CryptographicOperations.FixedTimeEquals(derived, expectedHash)
                ? Task.FromResult(AdminCredentialValidationResult.Success(canonicalUsername))
                : Task.FromResult(AdminCredentialValidationResult.InvalidCredentials());
        }
        catch (FormatException ex)
        {
            _customLogger.MyLogger.Warning($"AdminCredentialValidator: Administrator credentials are misconfigured. Ensure hash and salt are Base64 strings: {ex}");
            return Task.FromResult(AdminCredentialValidationResult.NotConfigured());
        }
    }
    #endregion

    #region Private Methods
    // Check if is validator configured
    private static bool IsConfigured(AdminAuthOptions options) =>
           !string.IsNullOrWhiteSpace(options.Username)
        && !string.IsNullOrWhiteSpace(options.PasswordHash)
        && !string.IsNullOrWhiteSpace(options.PasswordSalt);
    #endregion
}
