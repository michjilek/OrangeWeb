using OP_Shared_Library.Struct;

namespace OP_Shared_Library.Interfaces;

public interface IContactFormSender
{
    Task<bool> SendAsync(ContactFormSubmission submission, CancellationToken cancellationToken = default);
}
