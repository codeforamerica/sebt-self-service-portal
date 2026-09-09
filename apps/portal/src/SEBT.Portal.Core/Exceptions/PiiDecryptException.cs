namespace SEBT.Portal.Core.Exceptions;

/// <summary>
/// Thrown when an encrypted payload cannot be decrypted (tampering, corruption, unknown key/version).
/// Plaintext-at-rest transitional values (<see cref="Services.IPiiSymmetricEncryption.IsEnvelope"/>) do not trigger this exception.
/// </summary>
public class PiiDecryptException : Exception
{
    public PiiDecryptException()
    {
    }

    public PiiDecryptException(string message)
        : base(message)
    {
    }

    public PiiDecryptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
