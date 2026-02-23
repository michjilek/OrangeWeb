namespace Op_LP.Services.Admin;

// Configuration for the administrator credential validator. The password must be stored as a PBKDF2 hash.
public sealed class AdminAuthOptions
{
    #region Const
    // How many times hash function was run (it is only default value)
    public const int DefaultIterationCount = 120_000;
    #endregion

    #region Public Properties
    // Gets or sets the administrator user name. Comparison is case-insensitive.
    public string? Username { get; set; }

    // Gets or sets the PBKDF2 hash of the administrator password encoded in Base64.
    public string? PasswordHash { get; set; }

    // Gets or sets the salt used to create the PBKDF2 hash, encoded in Base64.
    public string? PasswordSalt { get; set; }

    // Gets or sets the PBKDF2 iteration count.
    public int IterationCount { get; set; } = DefaultIterationCount;
    #endregion
}
