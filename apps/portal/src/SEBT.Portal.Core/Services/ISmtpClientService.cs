namespace SEBT.Portal.Core.Services;

/// <summary>
/// Represents an embedded image for email with Content-ID reference.
/// </summary>
/// <param name="ContentId">The Content-ID used to reference the image in HTML (e.g., "logo" for src="cid:logo").</param>
/// <param name="Data">The image data as a byte array.</param>
/// <param name="ContentType">The MIME content type (e.g., "image/png").</param>
/// <param name="FileName">The filename for the attachment.</param>
public record EmailLinkedResource(string ContentId, byte[] Data, string ContentType, string FileName);

public interface ISmtpClientService
{
    Task SendEmailAsync(string to, string from, string subject, string body);

    Task SendEmailAsync(string to, string from, string subject, string body, IEnumerable<EmailLinkedResource> linkedResources);
}
