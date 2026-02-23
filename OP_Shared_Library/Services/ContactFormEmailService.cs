using Microsoft.Extensions.Options;
using OP_Shared_Library.Configurations;
using OP_Shared_Library.Interfaces;
using OP_Shared_Library.Struct;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace OP_Shared_Library.Services;

public class ContactFormEmailService(IOptions<ContactFormEmailOptions> options,
                                     IOptions<CompanyBrandingOptions>? brandingOptions = null,
                                     ICustomLogger customLogger = null
                                     ) : IContactFormSender
{
    #region Private Properties
    // Configuration Options
    private readonly ContactFormEmailOptions _options = options.Value;
    private readonly CompanyBrandingOptions _brandingOptions = brandingOptions?.Value ?? new CompanyBrandingOptions();
    private readonly ICustomLogger _customLogger = customLogger;
    #endregion

    #region Private Methods
    // Build Email Body
    private string BuildBody(ContactFormSubmission s)
    {
        return $"""
                    Jméno: {s.FullName}
                    Telefon: {s.Phone}
                    Email: {s.Email}
                    
                    Zpráva:
                    {s.Message}
                    """.Trim();
    }
    private string ResolveFromAddress(ContactFormSubmission submission)
    {
        // Use configured From Address
        if (!string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return _options.FromAddress;
        }

        // Fallback to Username
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            return _options.Username;
        }

        _customLogger.MyLogger.Error($"ContactFormEmailService: ResolveFromAddress: From Address or USername was not filled.");
        return string.Empty;
    }
    #endregion

    #region Public Methods
    public async Task<bool> SendAsync(ContactFormSubmission submission,
                          CancellationToken cancellationToken = default
                          )
    {
        // Validate HOST
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _customLogger.MyLogger.Error("ContactFormEmailService: SendAsync: Contact form email host is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ToAddress))
        {
            _customLogger.MyLogger.Error("ContactFormEmailService: SendAsync: Contact form email ToAddress is not configured.");
            return false;
        }

        try
        {
            // Create Mail Message
            using var message = new MailMessage
            {
                Subject = $"Zpráva z {_brandingOptions.ContactFormSource} od: {submission.FullName}",
                Body = BuildBody(submission),
                IsBodyHtml = false,
                From = new MailAddress(ResolveFromAddress(submission), submission.FullName),
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            // Add Recipient
            message.To.Add(new MailAddress(_options.ToAddress));

            // Sometimes helps because of strange language
            message.Headers.Add("Content-Language", "cs");

            // Reply to
            if (!string.IsNullOrWhiteSpace(submission.Email))
            {
                try
                {
                    message.ReplyToList.Add(new MailAddress(submission.Email, submission.FullName));
                }
                catch (FormatException)
                {
                    _customLogger?.MyLogger.Warning($"ContactFormEmailService: Invalid Reply-To email: {submission.Email}");
                }
            }

            // Create SMTP Client
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
            };

            // Set Credentials
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

            // Send Email
            await client.SendMailAsync(message, cancellationToken);

            // Info about sending something
            var nl = Environment.NewLine;
            _customLogger.MyLogger.Information(
                $"ContactFormEmailService: SendAsync: The email has been sent.{nl}" +
                $"Name: {submission.FullName}{nl}" +
                $"Phone: {submission.Phone}{nl}" +
                $"From: {submission.Email}{nl}" +
                $"Message: {submission.Message}"
            );

            return true;
        }
        catch (OperationCanceledException)
        {
            // Log cancellation
            _customLogger.MyLogger.Warning("ContactFormEmailService: SendAsync: Sending cancelled.");
            return false;
        }
        // Catch SMTP specific exceptions
        catch (SmtpException ex)
        {
            _customLogger.MyLogger.Error(ex, "ContactFormEmailService: SendAsync: SMTP error while sending email.");
            return false;
        }
        // Catch general exceptions
        catch (Exception ex)
        {
            _customLogger.MyLogger.Error(ex, "ContactFormEmailService: SendAsync: Unexpected error while sending email.");
            return false;
        }
    }
}
#endregion

#region Other Classes
// Contact Form Email Options
public sealed class ContactFormEmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; }
}
#endregion
