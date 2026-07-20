using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// Returns the configured override phone when in Development and config is set; otherwise null.
/// </summary>
public class DevelopmentPhoneOverrideProvider : IPhoneOverrideProvider
{
    private readonly IHostEnvironment _environment;
    private readonly DevelopmentPhoneOverrideOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevelopmentPhoneOverrideProvider"/> class.
    /// </summary>
    public DevelopmentPhoneOverrideProvider(IHostEnvironment environment, IOptions<DevelopmentPhoneOverrideOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string? GetOverridePhone()
    {
        if (!_environment.IsDevelopment())
        {
            return null;
        }

        var phone = _options.Phone?.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 10 ? digits : null;
    }
}
