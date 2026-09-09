using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoStateMetadataService : IStateMetadataService
{
    private static readonly StateMetadata Instance = new()
    {
        Name = "Colorado",
    };

    public Task<StateMetadata> GetStateMetadata() => Task.FromResult(Instance);
}
