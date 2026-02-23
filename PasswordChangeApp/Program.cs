using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

class Program
{
    static void Main()
    {
        // Ask user to enter a new password
        Console.Write("New password: ");
        var pwd = ReadPassword();
        Console.WriteLine();

        // Ask user to repeat the password for confirmation
        Console.Write("Repeat password: ");
        var pwd2 = ReadPassword();
        Console.WriteLine();

        // If passwords do not match, stop the program
        if (pwd != pwd2)
        {
            Console.WriteLine("Passwords do not match.");
            return;
        }

        // PBKDF2 parameters
        int iterations = 120_000;      // Number of iterations (higher = more secure, but slower)
        int hashBytes = 32;            // 32 bytes = 256-bit hash length
        int saltBytes = 16;            // 16 bytes = 128-bit salt length

        // Generate a new random salt
        var salt = new byte[saltBytes];
        RandomNumberGenerator.Fill(salt);

        // Derive a cryptographic hash using PBKDF2 (HMACSHA256)
        var hash = KeyDerivation.Pbkdf2(
            password: pwd,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: iterations,
            numBytesRequested: hashBytes);

        // Convert salt and hash to Base64 strings for easy storage
        string b64Salt = Convert.ToBase64String(salt);
        string b64Hash = Convert.ToBase64String(hash);

        // Print out configuration snippet ready to copy into appsettings.json
        Console.WriteLine();
        Console.WriteLine("Paste this into your config:");
        Console.WriteLine($@"""AdminAuth"": {{
                               ""Username"": ""Admin"",
                               ""PasswordHash"": ""{b64Hash}"",
                               ""PasswordSalt"": ""{b64Salt}"",
                               ""IterationCount"": {iterations}
                           }}");
    }

    // Reads a password from the console without echoing characters.
    // Returns the entered password as a string.
    static string ReadPassword()
    {
        // Using string for simplicity.
        var pwd = string.Empty;

        ConsoleKeyInfo key;

        // ReadKey(intercept: true) prevents the key from being displayed (no echo).
        // Loop until the user presses Enter, which signals end of input.
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            // If the user presses Backspace and there is at least one character,
            // remove the last character from the accumulator.
            if (key.Key == ConsoleKey.Backspace && pwd.Length > 0)
            {
                // Slice notation: remove the last character.
                pwd = pwd[..^1];

                continue;
            }

            // Ignore control keys (arrows, function keys, modifiers, etc.).
            // Only append printable characters to the password buffer.
            if (!char.IsControl(key.KeyChar))
            {
                // Append the typed character to the in-memory buffer.
                // This supports any Unicode character the console delivers.
                pwd += key.KeyChar;
            }
        }

        // User pressed Enter: finalize and return the accumulated password.
        Console.WriteLine(); // Move to the next line after Enter for clean output.
        return pwd;
    }

}
