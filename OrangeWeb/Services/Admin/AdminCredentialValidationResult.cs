namespace Op_LP.Services.Admin;

// Represents the result of validating administrator credentials.
// "Succeeded" - True when the credentials are valid.
// "IsConfigured" - True when the validator has all required configuration values.
// "CanonicalUsername" - The canonical administrator username when the validation succeeded.

// This is record definition and also primary ctor
public sealed record AdminCredentialValidationResult(bool Succeeded, bool IsConfigured, string? CanonicalUsername)
{
    #region Static Factory Methods
    public static AdminCredentialValidationResult Success(string canonicalUsername) =>
        new(Succeeded: true, IsConfigured: true, CanonicalUsername: canonicalUsername);

    public static AdminCredentialValidationResult InvalidCredentials() =>
        new(Succeeded: false, IsConfigured: true, CanonicalUsername: null);

    public static AdminCredentialValidationResult NotConfigured() =>
        new(Succeeded: false, IsConfigured: false, CanonicalUsername: null);
    #endregion
}
