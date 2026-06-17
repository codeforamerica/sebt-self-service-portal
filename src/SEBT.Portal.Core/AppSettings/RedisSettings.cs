namespace SEBT.Portal.Core.AppSettings;

public class RedisSettings
{
    public static readonly string SectionName = "Redis";

    /// <summary>Redis server hostname or IP address.</summary>
    public string? Host { get; set; }

    /// <summary>Redis server port. Defaults to 6379.</summary>
    public int Port { get; set; } = 6379;

    /// <summary>Optional password for Redis AUTH.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether to use TLS. Required for AWS Elasticache with in-transit encryption enabled.
    /// </summary>
    public bool Ssl { get; set; } = false;

    /// <summary>
    /// Expected hostname in the Redis server's TLS certificate.
    /// Required when Ssl is true and the certificate CN differs from Host,
    /// which is common with AWS Elasticache cluster endpoints.
    /// </summary>
    public string? SslHost { get; set; }

    /// <summary>Returns true when a host has been configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
