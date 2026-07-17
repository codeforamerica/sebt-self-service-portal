using System.Diagnostics;

namespace SEBT.Portal.Kernel.Telemetry;

public interface IInstrumentationSource
{
    ActivitySource ActivitySource { get; }
}
